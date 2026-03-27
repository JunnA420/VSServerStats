using System.Text.Json;
using System.Text.Json.Nodes;
using Vintagestory.API.Server;
using VSServerStats.Shared.Models;

namespace VSServerStats.Mod;

public class BanTracker : IDisposable
{
    private readonly ICoreServerAPI _sapi;
    private readonly string _banFilePath;
    private readonly string _serverConfigPath;
    private readonly object _lock = new();
    private readonly List<BanRecord> _bans = new();

    public BanTracker(ICoreServerAPI api)
    {
        _sapi = api;
        _banFilePath      = Path.Combine(api.DataBasePath, "ModData", "vsserverstats-bans.json");
        _serverConfigPath = Path.Combine(api.DataBasePath, "serverconfig.json");
        LoadFromDisk();
    }

    public AdminActionResponse BanPlayer(AdminActionRequest req)
    {
        try
        {
            DateTime? expires = req.DurationHours > 0
                ? DateTime.UtcNow.AddHours(req.DurationHours)
                : null;

            var record = new BanRecord
            {
                PlayerUid  = req.PlayerUid,
                PlayerName = req.PlayerName,
                Reason     = req.Reason,
                BannedAt   = DateTime.UtcNow,
                ExpiresAt  = expires,
                Active     = true
            };

            lock (_lock) _bans.Add(record);
            SaveToDisk();

            AddToVsBanList(req.PlayerUid, req.PlayerName, req.Reason, expires);

            // Kick if online (must run on main thread)
            if (_sapi.World.AllOnlinePlayers.Any(p => p.PlayerUID == req.PlayerUid))
            {
                var banUid = req.PlayerUid;
                var kickMsg = string.IsNullOrEmpty(req.Reason) ? "Byl jsi zabanován." : $"Ban: {req.Reason}";
                _sapi.Event.EnqueueMainThreadTask(() =>
                {
                    var op = _sapi.World.AllOnlinePlayers.FirstOrDefault(x => x.PlayerUID == banUid) as IServerPlayer;
                    op?.Disconnect(kickMsg);
                }, "vsserverstats-ban");
            }

            return new AdminActionResponse { Success = true, Message = $"Hráč {req.PlayerName} byl zabanován." };
        }
        catch (Exception ex)
        {
            return new AdminActionResponse { Success = false, Message = ex.Message };
        }
    }

    public AdminActionResponse UnbanPlayer(string playerUid)
    {
        try
        {
            lock (_lock)
            {
                foreach (var b in _bans.Where(b => b.PlayerUid == playerUid && b.Active))
                    b.Active = false;
            }
            SaveToDisk();
            RemoveFromVsBanList(playerUid);
            return new AdminActionResponse { Success = true, Message = "Hráč byl odbanován." };
        }
        catch (Exception ex)
        {
            return new AdminActionResponse { Success = false, Message = ex.Message };
        }
    }

    public AdminActionResponse KickPlayer(AdminActionRequest req)
    {
        try
        {
            var isOnline = _sapi.World.AllOnlinePlayers.Any(p => p.PlayerUID == req.PlayerUid);
            if (!isOnline)
                return new AdminActionResponse { Success = false, Message = "Hráč není online." };

            var uid = req.PlayerUid;
            var msg = string.IsNullOrWhiteSpace(req.Reason) ? "Byl jsi kicknut administrátorem." : req.Reason;
            _sapi.Event.EnqueueMainThreadTask(() =>
            {
                var p = _sapi.World.AllOnlinePlayers.FirstOrDefault(x => x.PlayerUID == uid) as IServerPlayer;
                p?.Disconnect(msg);
            }, "vsserverstats-kick");
            return new AdminActionResponse { Success = true, Message = $"Hráč {req.PlayerName} byl kicknut." };
        }
        catch (Exception ex)
        {
            return new AdminActionResponse { Success = false, Message = ex.Message };
        }
    }

    public List<BanRecord> GetBans(string? playerUid = null)
    {
        lock (_lock)
        {
            return playerUid == null
                ? _bans.ToList()
                : _bans.Where(b => b.PlayerUid == playerUid).ToList();
        }
    }

    // ── VS serverconfig manipulation ──────────────────────────────────────────

    private void AddToVsBanList(string uid, string name, string reason, DateTime? expires)
    {
        try
        {
            if (!File.Exists(_serverConfigPath)) return;
            var json = JsonNode.Parse(File.ReadAllText(_serverConfigPath))!;
            var bans = json["Bans"]?.AsArray() ?? new JsonArray();

            // Remove existing entry for this uid first
            for (int i = bans.Count - 1; i >= 0; i--)
                if (bans[i]?["PlayerUID"]?.GetValue<string>() == uid)
                    bans.RemoveAt(i);

            var entry = new JsonObject
            {
                ["PlayerUID"]  = uid,
                ["PlayerName"] = name,
                ["Reason"]     = reason,
                ["BanUntil"]   = expires?.ToString("o") ?? ""
            };
            bans.Add(entry);
            json["Bans"] = bans;
            File.WriteAllText(_serverConfigPath, json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            _sapi.Logger.Warning("[VSServerStats] Could not write VS ban list: " + ex.Message);
        }
    }

    private void RemoveFromVsBanList(string uid)
    {
        try
        {
            if (!File.Exists(_serverConfigPath)) return;
            var json = JsonNode.Parse(File.ReadAllText(_serverConfigPath))!;
            var bans = json["Bans"]?.AsArray();
            if (bans == null) return;
            for (int i = bans.Count - 1; i >= 0; i--)
                if (bans[i]?["PlayerUID"]?.GetValue<string>() == uid)
                    bans.RemoveAt(i);
            File.WriteAllText(_serverConfigPath, json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            _sapi.Logger.Warning("[VSServerStats] Could not remove from VS ban list: " + ex.Message);
        }
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    private void LoadFromDisk()
    {
        try
        {
            if (!File.Exists(_banFilePath)) return;
            var list = JsonSerializer.Deserialize<List<BanRecord>>(File.ReadAllText(_banFilePath));
            if (list == null) return;
            _bans.AddRange(list);
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error("[VSServerStats] Failed to load bans: " + ex.Message);
        }
    }

    private void SaveToDisk()
    {
        lock (_lock)
        {
            try
            {
                var dir = Path.GetDirectoryName(_banFilePath)!;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(_banFilePath, JsonSerializer.Serialize(_bans));
            }
            catch (Exception ex)
            {
                _sapi.Logger.Error("[VSServerStats] Failed to save bans: " + ex.Message);
            }
        }
    }

    public void Dispose() => SaveToDisk();
}
