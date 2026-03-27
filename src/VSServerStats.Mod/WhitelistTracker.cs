using System.Text.Json;
using System.Text.Json.Serialization;
using Vintagestory.API.Server;
using VSServerStats.Shared.Models;

namespace VSServerStats.Mod;

public class WhitelistTracker : IDisposable
{
    private readonly ICoreServerAPI _sapi;
    private readonly string _filePath;
    private readonly string _vsWhitelistPath;
    private readonly object _lock = new();
    private readonly List<WhitelistEntry> _entries = new();

    // Matches the VS playerswhitelisted.json format
    private class VsWhitelistEntry
    {
        [JsonPropertyName("PlayerUID")]  public string PlayerUID  { get; set; } = "";
        [JsonPropertyName("PlayerName")] public string PlayerName { get; set; } = "";
        [JsonPropertyName("UntilDate")]  public string UntilDate  { get; set; } = "";
        [JsonPropertyName("Reason")]     public string? Reason    { get; set; }
        [JsonPropertyName("IssuedByPlayerName")] public string IssuedByPlayerName { get; set; } = "Console";
    }

    public WhitelistTracker(ICoreServerAPI api)
    {
        _sapi            = api;
        _filePath        = Path.Combine(api.DataBasePath, "ModData", "vsserverstats-whitelist.json");
        _vsWhitelistPath = Path.Combine(api.DataBasePath, "Playerdata", "playerswhitelisted.json");
        LoadFromDisk();
    }

    public List<WhitelistEntry> GetAll()
    {
        lock (_lock)
        {
            MergeFromVsWhitelist();
            return _entries.ToList();
        }
    }

    public AdminActionResponse AddPlayer(string uid, string name)
    {
        try
        {
            lock (_lock)
            {
                if (_entries.Any(e => e.PlayerUid == uid))
                    return new AdminActionResponse { Success = false, Message = "Hráč je již na whitelistu." };

                _entries.Add(new WhitelistEntry
                {
                    PlayerUid  = uid,
                    PlayerName = name,
                    AddedAt    = DateTime.UtcNow
                });
            }
            SaveToDisk();
            AddToVsWhitelist(uid, name);
            return new AdminActionResponse { Success = true, Message = $"Hráč {name} přidán na whitelist." };
        }
        catch (Exception ex)
        {
            return new AdminActionResponse { Success = false, Message = ex.Message };
        }
    }

    public AdminActionResponse RemovePlayer(string uid)
    {
        try
        {
            lock (_lock)
            {
                var entry = _entries.FirstOrDefault(e => e.PlayerUid == uid);
                if (entry == null)
                    return new AdminActionResponse { Success = false, Message = "Hráč není na whitelistu." };
                _entries.Remove(entry);
            }
            SaveToDisk();
            RemoveFromVsWhitelist(uid);
            return new AdminActionResponse { Success = true, Message = "Hráč odebrán z whitelistu." };
        }
        catch (Exception ex)
        {
            return new AdminActionResponse { Success = false, Message = ex.Message };
        }
    }

    // ── VS playerswhitelisted.json manipulation ────────────────────────────────

    private void AddToVsWhitelist(string uid, string name)
    {
        try
        {
            var entries = ReadVsWhitelistRaw();
            if (!entries.Any(e => e.PlayerUID == uid))
            {
                entries.Add(new VsWhitelistEntry
                {
                    PlayerUID          = uid,
                    PlayerName         = name,
                    UntilDate          = DateTime.Now.AddYears(50).ToString("O"),
                    IssuedByPlayerName = "Console"
                });
                File.WriteAllText(_vsWhitelistPath, JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
            }
        }
        catch (Exception ex)
        {
            _sapi.Logger.Warning("[VSServerStats] Could not write VS whitelist: " + ex.Message);
        }
    }

    private void RemoveFromVsWhitelist(string uid)
    {
        try
        {
            var entries = ReadVsWhitelistRaw();
            entries.RemoveAll(e => e.PlayerUID == uid);
            File.WriteAllText(_vsWhitelistPath, JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            _sapi.Logger.Warning("[VSServerStats] Could not remove from VS whitelist: " + ex.Message);
        }
    }

    private List<VsWhitelistEntry> ReadVsWhitelistRaw()
    {
        if (!File.Exists(_vsWhitelistPath)) return new();
        return JsonSerializer.Deserialize<List<VsWhitelistEntry>>(File.ReadAllText(_vsWhitelistPath)) ?? new();
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    private void LoadFromDisk()
    {
        try
        {
            if (!File.Exists(_filePath)) return;
            var list = JsonSerializer.Deserialize<List<WhitelistEntry>>(File.ReadAllText(_filePath));
            if (list == null) return;
            _entries.AddRange(list);
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error("[VSServerStats] Failed to load whitelist: " + ex.Message);
        }
    }

    private void MergeFromVsWhitelist()
    {
        try
        {
            var vsEntries = ReadVsWhitelistRaw();
            bool changed = false;
            foreach (var e in vsEntries)
            {
                if (string.IsNullOrEmpty(e.PlayerUID)) continue;
                if (_entries.Any(x => x.PlayerUid == e.PlayerUID)) continue;
                _entries.Add(new WhitelistEntry
                {
                    PlayerUid  = e.PlayerUID,
                    PlayerName = e.PlayerName,
                    AddedAt    = DateTime.UtcNow
                });
                changed = true;
            }
            if (changed) SaveToDisk();
        }
        catch (Exception ex)
        {
            _sapi.Logger.Warning("[VSServerStats] Could not read VS whitelist: " + ex.Message);
        }
    }

    private void SaveToDisk()
    {
        lock (_lock)
        {
            try
            {
                var dir = Path.GetDirectoryName(_filePath)!;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(_filePath, JsonSerializer.Serialize(_entries));
            }
            catch (Exception ex)
            {
                _sapi.Logger.Error("[VSServerStats] Failed to save whitelist: " + ex.Message);
            }
        }
    }

    public void Dispose() => SaveToDisk();
}
