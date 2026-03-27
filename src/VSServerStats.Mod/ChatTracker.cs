using System.Text.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using VSServerStats.Shared.Models;

namespace VSServerStats.Mod;

public class ChatTracker : IDisposable
{
    private readonly ICoreServerAPI _sapi;
    private readonly string _chatFilePath;
    private readonly object _lock = new();

    // uid → list of messages (capped at 500 per player)
    private readonly Dictionary<string, List<ChatMessage>> _chats = new();

    public ChatTracker(ICoreServerAPI api)
    {
        _sapi = api;
        _chatFilePath = Path.Combine(api.DataBasePath, "ModData", "vsserverstats-chat.json");
        LoadFromDisk();
        api.Event.PlayerChat += OnPlayerChat;
    }

    private void OnPlayerChat(IServerPlayer player, int channelId, ref string message, ref string data, BoolRef consumed)
    {
        var uid = player.PlayerUID;
        var entry = new ChatMessage
        {
            PlayerUid  = uid,
            PlayerName = player.PlayerName,
            Message    = message,
            Timestamp  = DateTime.UtcNow
        };

        lock (_lock)
        {
            if (!_chats.TryGetValue(uid, out var list))
            {
                list = new List<ChatMessage>();
                _chats[uid] = list;
            }
            list.Add(entry);
            // cap per player
            if (list.Count > 500)
                list.RemoveRange(0, list.Count - 500);
        }

        SaveToDisk();
    }

    public List<ChatMessage> GetMessages(string playerUid)
    {
        lock (_lock)
        {
            return _chats.TryGetValue(playerUid, out var list)
                ? list.AsReadOnly().ToList()
                : new List<ChatMessage>();
        }
    }

    /// <summary>Import messages parsed from a log file upload. Merges by timestamp dedup.</summary>
    public void ImportMessages(List<ChatMessage> messages)
    {
        lock (_lock)
        {
            foreach (var msg in messages)
            {
                if (!_chats.TryGetValue(msg.PlayerUid, out var list))
                {
                    list = new List<ChatMessage>();
                    _chats[msg.PlayerUid] = list;
                }
                // dedup by timestamp + message
                if (!list.Any(m => m.Timestamp == msg.Timestamp && m.Message == msg.Message))
                    list.Add(msg);
            }
            // sort and cap
            foreach (var list in _chats.Values)
            {
                list.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
                if (list.Count > 500)
                    list.RemoveRange(0, list.Count - 500);
            }
        }
        SaveToDisk();
    }

    private void LoadFromDisk()
    {
        try
        {
            if (!File.Exists(_chatFilePath)) return;
            var json = File.ReadAllText(_chatFilePath);
            var data = JsonSerializer.Deserialize<Dictionary<string, List<ChatMessage>>>(json);
            if (data == null) return;
            foreach (var kv in data)
                _chats[kv.Key] = kv.Value;
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error("[VSServerStats] Failed to load chat: " + ex.Message);
        }
    }

    private void SaveToDisk()
    {
        lock (_lock)
        {
            try
            {
                var dir = Path.GetDirectoryName(_chatFilePath)!;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var json = JsonSerializer.Serialize(_chats);
                File.WriteAllText(_chatFilePath, json);
            }
            catch (Exception ex)
            {
                _sapi.Logger.Error("[VSServerStats] Failed to save chat: " + ex.Message);
            }
        }
    }

    public void Dispose()
    {
        SaveToDisk();
    }
}
