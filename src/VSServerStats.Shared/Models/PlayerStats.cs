namespace VSServerStats.Shared.Models;

public class PlayerStats
{
    public string PlayerUid { get; set; } = "";
    public string PlayerName { get; set; } = "";
    public int Deaths { get; set; }
    public int PlayerKills { get; set; }
    public double PlaytimeSeconds { get; set; }
    public double DistanceWalkedMeters { get; set; }
    public DateTime LastSeen { get; set; }
    public DateTime FirstSeen { get; set; }
    public int TotalXSkillsLevel { get; set; }
    public Dictionary<string, int> XSkillsLevels { get; set; } = new();
    public Dictionary<string, Dictionary<string, int>> XSkillsAbilities { get; set; } = new();
}
