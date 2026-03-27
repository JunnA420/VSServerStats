using System.Collections.Concurrent;
using System.Text.Json;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using VSServerStats.Shared.Models;

namespace VSServerStats.Mod;

public class SessionRecord
{
    public string PlayerUid { get; set; } = "";
    public DateTime JoinTime { get; set; }
    public DateTime LeaveTime { get; set; }
}

public class StatsTracker : IDisposable
{
    private readonly ICoreServerAPI _sapi;
    private readonly ConcurrentDictionary<string, PlayerStats> _stats = new();
    private readonly ConcurrentDictionary<string, Vec3d> _lastPositions = new();
    private readonly ConcurrentDictionary<string, DateTime> _sessionStart = new();
    private readonly ConcurrentDictionary<string, PlayerAppearance> _appearances = new();
    private readonly List<SessionRecord> _sessions = new();

    private readonly string _saveFilePath;
    private readonly string _appearanceFilePath;
    private readonly string _sessionFilePath;
    private readonly object _saveLock = new();
    private readonly bool _xSkillsPresent;

    // Dirty flags — avoid writing to disk unless data actually changed
    private volatile bool _statsDirty;
    private volatile bool _appearanceDirty;
    private volatile bool _sessionsDirty;

    // xSkills cache — re-read only every 60s
    private Dictionary<string, XSkillsSavedSkillSet> _xSkillsCache = new();
    private DateTime _xSkillsCacheTime = DateTime.MinValue;
    private static readonly TimeSpan XSkillsCacheTtl = TimeSpan.FromSeconds(60);

    public StatsTracker(ICoreServerAPI api)
    {
        _sapi = api;
        _saveFilePath = Path.Combine(api.DataBasePath, "ModData", "vsserverstats.json");
        _appearanceFilePath = Path.Combine(api.DataBasePath, "ModData", "vsserverstats-appearance.json");
        _sessionFilePath = Path.Combine(api.DataBasePath, "ModData", "vsserverstats-sessions.json");

        _xSkillsPresent = api.ModLoader.IsModEnabled("xskills");
        api.Logger.Notification("[VSServerStats] xSkills present: " + _xSkillsPresent);

        LoadFromDisk();
        LoadAppearanceFromDisk();
        LoadSessionsFromDisk();

        api.Event.PlayerDeath += OnPlayerDeath;
        api.Event.PlayerNowPlaying += OnPlayerJoin;
        api.Event.PlayerDisconnect += OnPlayerLeave;
        api.Event.RegisterGameTickListener(OnTick, 5000);         // every 5s
    }

    private void OnPlayerJoin(IServerPlayer player)
    {
        var uid = player.PlayerUID;
        var now = DateTime.UtcNow;

        _stats.AddOrUpdate(uid,
            _ => new PlayerStats
            {
                PlayerUid = uid,
                PlayerName = player.PlayerName,
                FirstSeen = now,
                LastSeen = now
            },
            (_, existing) =>
            {
                existing.PlayerName = player.PlayerName;
                existing.LastSeen = now;
                return existing;
            });

        _sessionStart[uid] = now;

        var pos = player.Entity?.Pos?.XYZ;
        if (pos != null)
            _lastPositions[uid] = pos.Clone();

        UpdateAppearance(player);
    }

    private static readonly JsonSerializerOptions _caseInsensitive = new() { PropertyNameCaseInsensitive = true };
    private class XSkillsSavedSkillSet { public Dictionary<string, XSkillsSavedSkill>? Skills { get; set; } }
    private class XSkillsSavedSkill { public int Level { get; set; } public Dictionary<string, XSkillsSavedAbility>? Abilities { get; set; } }
    private class XSkillsSavedAbility { public int Tier { get; set; } }

    private void UpdateAppearance(IServerPlayer player)
    {
        var uid = player.PlayerUID;
        var appearance = new PlayerAppearance { PlayerUid = uid };

        try
        {
            var watched = player.Entity?.WatchedAttributes;
            if (watched == null) return;

            var skinParts = watched.GetTreeAttribute("skinConfig")?.GetTreeAttribute("appliedParts");
            if (skinParts == null) return;

            appearance.SkinColor         = skinParts.GetString("baseskin", "");
            appearance.HairColor         = skinParts.GetString("haircolor", "");
            appearance.HairType          = skinParts.GetString("hairbase", "");
            appearance.HairExtra         = skinParts.GetString("hairextra", "");
            appearance.EyeColor          = skinParts.GetString("eyecolor", "");
            appearance.FacialExpression  = skinParts.GetString("facialexpression", "");
            appearance.FacialHair        = skinParts.GetString("mustache", "");
            appearance.Beard             = skinParts.GetString("beard", "");
            appearance.CharacterClass    = player.Entity?.WatchedAttributes?.GetString("characterClass", "") ?? "";
        }
        catch (Exception ex)
        {
            _sapi.Logger.Warning("[VSServerStats] Could not read appearance for " + player.PlayerName + ": " + ex.Message);
            return;
        }

        _appearances[uid] = appearance;
        _appearanceDirty = true;
    }

    private void OnPlayerLeave(IServerPlayer player)
    {
        var uid = player.PlayerUID;
        if (_sessionStart.TryGetValue(uid, out var joinTime))
        {
            var session = new SessionRecord { PlayerUid = uid, JoinTime = joinTime, LeaveTime = DateTime.UtcNow };
            lock (_saveLock) { _sessions.Add(session); TrimSessions(); }
            _sessionsDirty = true;
        }
        FlushPlaytime(uid);
        _lastPositions.TryRemove(uid, out _);
        _sessionStart.TryRemove(uid, out _);
        _statsDirty = true;
    }

    private void OnPlayerDeath(IServerPlayer player, DamageSource damageSource)
    {
        var uid = player.PlayerUID;
        if (_stats.TryGetValue(uid, out var stats))
            stats.Deaths++;

        var killerPlayer = damageSource.SourceEntity as IServerPlayer;
        if (killerPlayer != null && _stats.TryGetValue(killerPlayer.PlayerUID, out var killerStats))
            killerStats.PlayerKills++;

        _statsDirty = true;
    }

    private void OnTick(float dt)
    {
        foreach (var player in _sapi.World.AllOnlinePlayers)
        {
            var uid = player.PlayerUID;
            var entity = (player as IServerPlayer)?.Entity;
            if (entity == null) continue;

            var pos = entity.Pos?.XYZ;
            if (pos == null) continue;

            if (_lastPositions.TryGetValue(uid, out var last))
            {
                var dist = last.DistanceTo(pos);
                // Sanity cap: ignore teleports / chunks loading (>100m in 5s)
                if (dist < 100 && _stats.TryGetValue(uid, out var stats))
                {
                    stats.DistanceWalkedMeters += dist;
                    _statsDirty = true;
                }
            }

            _lastPositions[uid] = pos.Clone();

            // Refresh appearance once if skinConfig wasn't available at join time
            if (_appearances.TryGetValue(uid, out var app) && string.IsNullOrEmpty(app.SkinColor))
                UpdateAppearance((IServerPlayer)player);
        }

        if (_statsDirty)     { SaveToDisk();           _statsDirty      = false; }
        if (_appearanceDirty){ SaveAppearanceToDisk();  _appearanceDirty = false; }
        if (_sessionsDirty)  { SaveSessionsToDisk();    _sessionsDirty   = false; }
    }

    private void FlushPlaytime(string uid)
    {
        if (_sessionStart.TryGetValue(uid, out var start) && _stats.TryGetValue(uid, out var stats))
        {
            stats.PlaytimeSeconds += (DateTime.UtcNow - start).TotalSeconds;
            stats.LastSeen = DateTime.UtcNow;
        }
    }

    private Dictionary<string, XSkillsSavedSkillSet> ReadAllXSkills()
    {
        if (DateTime.UtcNow - _xSkillsCacheTime < XSkillsCacheTtl)
            return _xSkillsCache;

        try
        {
            var worldName = _sapi.WorldManager.SaveGame.WorldName;
            var sanitized = string.Concat(worldName.Split(Path.GetInvalidFileNameChars()));
            var filePath = Path.Combine(Vintagestory.API.Config.GamePaths.Saves, "XLeveling", sanitized + ".json");

            if (!File.Exists(filePath)) return _xSkillsCache;

            var json = File.ReadAllText(filePath);
            _xSkillsCache = JsonSerializer.Deserialize<Dictionary<string, XSkillsSavedSkillSet>>(json, _caseInsensitive) ?? new();
            _xSkillsCacheTime = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _sapi.Logger.Warning("[VSServerStats] Could not read xSkills: " + ex.Message);
        }
        return _xSkillsCache;
    }

    public List<PlayerStats> GetAllStats()
    {
        var xskills = _xSkillsPresent ? ReadAllXSkills() : new();

        var result = new List<PlayerStats>();
        foreach (var kv in _stats)
        {
            var copy = new PlayerStats
            {
                PlayerUid            = kv.Value.PlayerUid,
                PlayerName           = kv.Value.PlayerName,
                Deaths               = kv.Value.Deaths,
                PlayerKills          = kv.Value.PlayerKills,
                DistanceWalkedMeters = kv.Value.DistanceWalkedMeters,
                FirstSeen            = kv.Value.FirstSeen,
                LastSeen             = kv.Value.LastSeen,
                PlaytimeSeconds      = kv.Value.PlaytimeSeconds,
            };

            if (_sessionStart.TryGetValue(kv.Key, out var start))
                copy.PlaytimeSeconds += (DateTime.UtcNow - start).TotalSeconds;

            if (xskills.TryGetValue(kv.Key, out var skillSet) && skillSet.Skills != null)
            {
                copy.XSkillsLevels     = skillSet.Skills.ToDictionary(s => s.Key, s => s.Value.Level);
                copy.TotalXSkillsLevel = copy.XSkillsLevels.Values.Sum();
                copy.XSkillsAbilities  = skillSet.Skills.ToDictionary(
                    s => s.Key,
                    s => (s.Value.Abilities ?? new()).ToDictionary(a => a.Key, a => a.Value.Tier)
                );
            }

            result.Add(copy);
        }
        return result;
    }

    public List<PlayerAppearance> GetAllAppearances() => _appearances.Values.ToList();

    public VSServerStats.Shared.Models.HeatmapResponse GetHeatmap()
    {
        // data[dayOfWeek 0=Mon..6=Sun][hour]
        var data = Enumerable.Range(0, 7).Select(_ => new int[24]).ToArray();

        List<SessionRecord> snapshot;
        lock (_saveLock) snapshot = _sessions.ToList();

        // Also count currently online players as an ongoing session
        foreach (var p in _sapi.World.AllOnlinePlayers)
        {
            if (_sessionStart.TryGetValue(p.PlayerUID, out var joinTime))
                snapshot.Add(new SessionRecord { PlayerUid = p.PlayerUID, JoinTime = joinTime, LeaveTime = DateTime.UtcNow });
        }

        foreach (var s in snapshot)
        {
            // Walk through each hour slot the session overlaps
            var cur = new DateTime(s.JoinTime.Year, s.JoinTime.Month, s.JoinTime.Day, s.JoinTime.Hour, 0, 0, DateTimeKind.Utc);
            var end = s.LeaveTime;
            while (cur <= end)
            {
                int dow = ((int)cur.DayOfWeek + 6) % 7; // 0=Mon
                data[dow][cur.Hour]++;
                cur = cur.AddHours(1);
            }
        }

        int max = data.SelectMany(x => x).DefaultIfEmpty(0).Max();
        return new VSServerStats.Shared.Models.HeatmapResponse { Data = data, Max = max };
    }

    private void TrimSessions()
    {
        // Keep only last 90 days
        var cutoff = DateTime.UtcNow.AddDays(-90);
        _sessions.RemoveAll(s => s.LeaveTime < cutoff);
    }

    private void LoadSessionsFromDisk()
    {
        try
        {
            if (!File.Exists(_sessionFilePath)) return;
            var list = JsonSerializer.Deserialize<List<SessionRecord>>(File.ReadAllText(_sessionFilePath));
            if (list == null) return;
            lock (_saveLock) { _sessions.AddRange(list); TrimSessions(); }
        }
        catch (Exception ex) { _sapi.Logger.Error("[VSServerStats] Failed to load sessions: " + ex.Message); }
    }

    private void SaveSessionsToDisk()
    {
        lock (_saveLock)
        {
            try
            {
                var dir = Path.GetDirectoryName(_sessionFilePath)!;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(_sessionFilePath, JsonSerializer.Serialize(_sessions));
            }
            catch (Exception ex) { _sapi.Logger.Error("[VSServerStats] Failed to save sessions: " + ex.Message); }
        }
    }

    private void LoadAppearanceFromDisk()
    {
        try
        {
            if (!File.Exists(_appearanceFilePath)) return;
            var json = File.ReadAllText(_appearanceFilePath);
            var list = JsonSerializer.Deserialize<List<PlayerAppearance>>(json);
            if (list == null) return;
            foreach (var a in list)
                _appearances[a.PlayerUid] = a;
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error("[VSServerStats] Failed to load appearances: " + ex.Message);
        }
    }

    private void SaveAppearanceToDisk()
    {
        lock (_saveLock)
        {
            try
            {
                var dir = Path.GetDirectoryName(_appearanceFilePath)!;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var json = JsonSerializer.Serialize(_appearances.Values.ToList());
                File.WriteAllText(_appearanceFilePath, json);
            }
            catch (Exception ex)
            {
                _sapi.Logger.Error("[VSServerStats] Failed to save appearances: " + ex.Message);
            }
        }
    }

    private void LoadFromDisk()
    {
        try
        {
            if (!File.Exists(_saveFilePath)) return;
            var json = File.ReadAllText(_saveFilePath);
            var list = JsonSerializer.Deserialize<List<PlayerStats>>(json);
            if (list == null) return;
            foreach (var p in list)
                _stats[p.PlayerUid] = p;
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error("[VSServerStats] Failed to load stats: " + ex.Message);
        }
    }

    private void SaveToDisk()
    {
        lock (_saveLock)
        {
            try
            {
                var dir = Path.GetDirectoryName(_saveFilePath)!;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var json = JsonSerializer.Serialize(_stats.Values.ToList());
                File.WriteAllText(_saveFilePath, json);
            }
            catch (Exception ex)
            {
                _sapi.Logger.Error("[VSServerStats] Failed to save stats: " + ex.Message);
            }
        }
    }

    public void Dispose()
    {
        _sapi.Event.PlayerDeath       -= OnPlayerDeath;
        _sapi.Event.PlayerNowPlaying  -= OnPlayerJoin;
        _sapi.Event.PlayerDisconnect  -= OnPlayerLeave;

        foreach (var uid in _sessionStart.Keys)
            FlushPlaytime(uid);
        SaveToDisk();
        if (_appearanceDirty) SaveAppearanceToDisk();
        if (_sessionsDirty)   SaveSessionsToDisk();
    }
}
