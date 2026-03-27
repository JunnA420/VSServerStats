namespace VSServerStats.Shared.Models;

public class ChatMessage
{
    public string PlayerUid  { get; set; } = "";
    public string PlayerName { get; set; } = "";
    public string Message    { get; set; } = "";
    public DateTime Timestamp { get; set; }
}

public class BanRecord
{
    public string PlayerUid  { get; set; } = "";
    public string PlayerName { get; set; } = "";
    public string Reason     { get; set; } = "";
    public string BannedBy   { get; set; } = "admin";
    public DateTime BannedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool Active       { get; set; } = true;
}

public class WhitelistEntry
{
    public string PlayerUid  { get; set; } = "";
    public string PlayerName { get; set; } = "";
    public DateTime AddedAt  { get; set; }
    public string AddedBy    { get; set; } = "admin";
}

public class AdminActionRequest
{
    public string PlayerUid  { get; set; } = "";
    public string PlayerName { get; set; } = "";
    public string Reason     { get; set; } = "";
    /// <summary>Ban duration in hours. 0 = permanent.</summary>
    public int DurationHours { get; set; } = 0;
}

public class AdminActionResponse
{
    public bool   Success { get; set; }
    public string Message { get; set; } = "";
}

public class ChatLogResponse
{
    public string PlayerUid      { get; set; } = "";
    public List<ChatMessage> Messages { get; set; } = new();
}

public class BanListResponse
{
    public List<BanRecord> Bans { get; set; } = new();
}

public class WhitelistResponse
{
    public List<WhitelistEntry> Entries { get; set; } = new();
}

/// <summary>
/// Activity heatmap: [dayOfWeek 0=Mon..6=Sun][hour 0..23] = session count
/// </summary>
public class HeatmapResponse
{
    /// <summary>7 days × 24 hours — number of player-sessions active in that slot</summary>
    public int[][] Data { get; set; } = Enumerable.Range(0, 7).Select(_ => new int[24]).ToArray();
    public int Max { get; set; }
}
