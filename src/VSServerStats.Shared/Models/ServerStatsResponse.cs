namespace VSServerStats.Shared.Models;

public class ServerStatsResponse
{
    public string ServerName { get; set; } = "";
    public List<string> OnlinePlayers { get; set; } = [];
    public List<PlayerStats> Players { get; set; } = [];
    public DateTime LastUpdated { get; set; }
}
