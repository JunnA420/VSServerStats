using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using VSServerStats.Shared.Models;

namespace VSServerStats.Web.Services;

public class AdminService
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _token;

    private static readonly JsonSerializerOptions _opts = new() { PropertyNameCaseInsensitive = true };

    public AdminService(HttpClient http, IConfiguration config)
    {
        _http    = http;
        _baseUrl = config["ModApiUrl"] ?? "http://localhost:1047";
        _token   = config["Dashboard:ModAdminToken"] ?? "";
    }

    // ── Players ───────────────────────────────────────────────────────────────

    public async Task<List<AdminPlayerRow>?> GetPlayersAsync()
    {
        try
        {
            var req = AdminRequest(HttpMethod.Get, "/admin/players");
            var res = await _http.SendAsync(req);
            var json = await res.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<AdminPlayerRow>>(json, _opts);
        }
        catch { return null; }
    }

    // ── Chat ──────────────────────────────────────────────────────────────────

    public async Task<ChatLogResponse?> GetChatLogAsync(string uid)
    {
        try
        {
            var req = AdminRequest(HttpMethod.Get, $"/admin/chatlog?uid={Uri.EscapeDataString(uid)}");
            var res = await _http.SendAsync(req);
            return JsonSerializer.Deserialize<ChatLogResponse>(await res.Content.ReadAsStringAsync(), _opts);
        }
        catch { return null; }
    }

    public async Task<AdminActionResponse?> ImportChatAsync(List<ChatMessage> messages)
    {
        try
        {
            var req = AdminRequest(HttpMethod.Post, "/admin/importchat");
            req.Content = new StringContent(JsonSerializer.Serialize(messages), Encoding.UTF8, "application/json");
            var res = await _http.SendAsync(req);
            return JsonSerializer.Deserialize<AdminActionResponse>(await res.Content.ReadAsStringAsync(), _opts);
        }
        catch { return null; }
    }

    // ── Bans ──────────────────────────────────────────────────────────────────

    public async Task<BanListResponse?> GetBansAsync(string? uid = null)
    {
        try
        {
            var url = uid == null ? "/admin/bans" : $"/admin/bans?uid={Uri.EscapeDataString(uid)}";
            var req = AdminRequest(HttpMethod.Get, url);
            var res = await _http.SendAsync(req);
            return JsonSerializer.Deserialize<BanListResponse>(await res.Content.ReadAsStringAsync(), _opts);
        }
        catch { return null; }
    }

    public async Task<AdminActionResponse?> BanPlayerAsync(AdminActionRequest action)
        => await PostActionAsync("/admin/ban", action);

    public async Task<AdminActionResponse?> UnbanPlayerAsync(string uid)
        => await PostActionAsync("/admin/unban", new AdminActionRequest { PlayerUid = uid });

    public async Task<AdminActionResponse?> KickPlayerAsync(AdminActionRequest action)
        => await PostActionAsync("/admin/kick", action);

    // ── Whitelist ─────────────────────────────────────────────────────────────

    public async Task<WhitelistResponse?> GetWhitelistAsync()
    {
        try
        {
            var req = AdminRequest(HttpMethod.Get, "/admin/whitelist");
            var res = await _http.SendAsync(req);
            return JsonSerializer.Deserialize<WhitelistResponse>(await res.Content.ReadAsStringAsync(), _opts);
        }
        catch { return null; }
    }

    public async Task<HeatmapResponse?> GetHeatmapAsync()
    {
        try
        {
            var req = AdminRequest(HttpMethod.Get, "/admin/heatmap");
            var res = await _http.SendAsync(req);
            return JsonSerializer.Deserialize<HeatmapResponse>(await res.Content.ReadAsStringAsync(), _opts);
        }
        catch { return null; }
    }

    public async Task<AdminActionResponse?> AddToWhitelistAsync(AdminActionRequest action)
        => await PostActionAsync("/admin/whitelist", action);

    public async Task<AdminActionResponse?> RemoveFromWhitelistAsync(string uid)
    {
        try
        {
            var req = AdminRequest(HttpMethod.Delete, $"/admin/whitelist?uid={Uri.EscapeDataString(uid)}");
            var res = await _http.SendAsync(req);
            return JsonSerializer.Deserialize<AdminActionResponse>(await res.Content.ReadAsStringAsync(), _opts);
        }
        catch { return null; }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<AdminActionResponse?> PostActionAsync(string path, AdminActionRequest action)
    {
        try
        {
            var req = AdminRequest(HttpMethod.Post, path);
            req.Content = new StringContent(JsonSerializer.Serialize(action), Encoding.UTF8, "application/json");
            var res = await _http.SendAsync(req);
            return JsonSerializer.Deserialize<AdminActionResponse>(await res.Content.ReadAsStringAsync(), _opts);
        }
        catch { return null; }
    }

    private HttpRequestMessage AdminRequest(HttpMethod method, string path)
    {
        var msg = new HttpRequestMessage(method, _baseUrl + path);
        msg.Headers.Add("X-Admin-Token", _token);
        return msg;
    }
}

// ── View model returned by /admin/players ─────────────────────────────────────

public class AdminPlayerRow
{
    public string PlayerUid           { get; set; } = "";
    public string PlayerName          { get; set; } = "";
    public double PlaytimeSeconds     { get; set; }
    public int    Deaths              { get; set; }
    public int    PlayerKills         { get; set; }
    public double DistanceWalkedMeters { get; set; }
    public DateTime FirstSeen         { get; set; }
    public DateTime LastSeen          { get; set; }
    public int    TotalXSkillsLevel   { get; set; }
    public bool   IsOnline            { get; set; }
    public bool   IsBanned            { get; set; }
}
