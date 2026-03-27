using System.Text.Json;
using VSServerStats.Shared.Models;

namespace VSServerStats.Web.Services;

public class StatsService
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public StatsService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _baseUrl = config["ModApiUrl"] ?? "http://localhost:5100";
    }

    public async Task<ServerStatsResponse?> GetStatsAsync()
    {
        try
        {
            var json = await _http.GetStringAsync(_baseUrl + "/stats");
            return JsonSerializer.Deserialize<ServerStatsResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<PlayerAppearance>?> GetAppearanceAsync()
    {
        try
        {
            var json = await _http.GetStringAsync(_baseUrl + "/appearance");
            return JsonSerializer.Deserialize<List<PlayerAppearance>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
    }
}
