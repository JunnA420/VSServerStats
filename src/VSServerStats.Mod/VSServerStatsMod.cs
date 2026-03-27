using System.Net;
using System.Text;
using System.Text.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using VSServerStats.Shared.Models;

namespace VSServerStats.Mod;

public class VSServerStatsMod : ModSystem
{
    private ICoreServerAPI _sapi = null!;
    private StatsTracker   _tracker     = null!;
    private ChatTracker    _chat        = null!;
    private BanTracker     _bans        = null!;
    private WhitelistTracker _whitelist = null!;
    private HttpListener   _listener    = null!;
    private CancellationTokenSource _cts = null!;

    private const int    Port        = 1047;
    private const string AdminPrefix = "/admin/";

    // Loaded from modconfig (ModData/vsserverstats-config.json) at startup
    private string _adminToken = "";

    private static readonly JsonSerializerOptions _jsonOpts = new() { WriteIndented = false };

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

    public override void StartServerSide(ICoreServerAPI api)
    {
        _sapi      = api;
        _tracker   = new StatsTracker(api);
        _chat      = new ChatTracker(api);
        _bans      = new BanTracker(api);
        _whitelist = new WhitelistTracker(api);

        LoadConfig();
        StartHttpListener();

        api.Logger.Notification("[VSServerStats] Started on port " + Port);
        if (string.IsNullOrEmpty(_adminToken))
            api.Logger.Warning("[VSServerStats] AdminToken is not set! Admin endpoints are disabled. Set it in ModData/vsserverstats-config.json.");
    }

    private void LoadConfig()
    {
        var path = Path.Combine(_sapi.DataBasePath, "ModData", "vsserverstats-config.json");
        if (!File.Exists(path))
        {
            // Write default config with a random token
            var dir = Path.GetDirectoryName(path)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            _adminToken = Guid.NewGuid().ToString("N");
            File.WriteAllText(path, JsonSerializer.Serialize(new { AdminToken = _adminToken }, new JsonSerializerOptions { WriteIndented = true }));
            _sapi.Logger.Notification("[VSServerStats] Generated new AdminToken: " + _adminToken);
            return;
        }
        try
        {
            var doc = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(path));
            _adminToken = doc.GetProperty("AdminToken").GetString() ?? "";
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error("[VSServerStats] Failed to load config: " + ex.Message);
        }
    }

    private void StartHttpListener()
    {
        _cts = new CancellationTokenSource();
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://*:{Port}/");
        _listener.Start();
        Task.Run(() => ListenLoop(_cts.Token));
    }

    private async Task ListenLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var ctx = await _listener.GetContextAsync();
                _ = Task.Run(() => HandleRequest(ctx));
            }
            catch (HttpListenerException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _sapi.Logger.Error("[VSServerStats] Listener error: " + ex.Message);
            }
        }
    }

    private void HandleRequest(HttpListenerContext ctx)
    {
        try
        {
            var path   = ctx.Request.Url?.AbsolutePath ?? "";
            var method = ctx.Request.HttpMethod.ToUpperInvariant();

            // Public endpoints
            if (path == "/stats" && method == "GET")   { HandleStats(ctx); return; }
            if (path == "/appearance" && method == "GET") { HandleAppearance(ctx); return; }

            // Admin endpoints — require token
            if (path.StartsWith("/admin/"))
            {
                if (!IsAuthorized(ctx)) { SendJson(ctx, 401, new AdminActionResponse { Success = false, Message = "Unauthorized" }); return; }

                if (path == "/admin/players"    && method == "GET")  { HandleAdminPlayers(ctx);    return; }
                if (path == "/admin/chatlog"    && method == "GET")  { HandleAdminChatLog(ctx);    return; }
                if (path == "/admin/bans"       && method == "GET")  { HandleAdminBans(ctx);       return; }
                if (path == "/admin/ban"        && method == "POST") { HandleAdminBan(ctx);        return; }
                if (path == "/admin/unban"      && method == "POST") { HandleAdminUnban(ctx);      return; }
                if (path == "/admin/kick"       && method == "POST") { HandleAdminKick(ctx);       return; }
                if (path == "/admin/whitelist"  && method == "GET")  { HandleAdminWhitelist(ctx);  return; }
                if (path == "/admin/whitelist"  && method == "POST") { HandleAdminWhitelistAdd(ctx); return; }
                if (path == "/admin/whitelist"  && method == "DELETE") { HandleAdminWhitelistRemove(ctx); return; }
                if (path == "/admin/importchat" && method == "POST") { HandleAdminImportChat(ctx); return; }
                if (path == "/admin/heatmap"    && method == "GET")  { SendJson(ctx, 200, _tracker.GetHeatmap()); return; }
            }

            ctx.Response.StatusCode = 404;
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error("[VSServerStats] Request error: " + ex.Message);
            try { ctx.Response.StatusCode = 500; } catch { }
        }
        finally
        {
            try { ctx.Response.Close(); } catch { }
        }
    }

    // ── Auth ─────────────────────────────────────────────────────────────────

    private bool IsAuthorized(HttpListenerContext ctx)
    {
        if (string.IsNullOrEmpty(_adminToken)) return false;
        var header = ctx.Request.Headers["X-Admin-Token"] ?? "";
        return header == _adminToken;
    }

    // ── Public ───────────────────────────────────────────────────────────────

    private void HandleStats(HttpListenerContext ctx)
    {
        var response = new ServerStatsResponse
        {
            ServerName    = _sapi.World.Seed.ToString(),
            OnlinePlayers = _sapi.World.AllOnlinePlayers.Select(p => p.PlayerName).ToList(),
            Players       = _tracker.GetAllStats(),
            LastUpdated   = DateTime.UtcNow
        };
        SendJson(ctx, 200, response);
    }

    private void HandleAppearance(HttpListenerContext ctx)
        => SendJson(ctx, 200, _tracker.GetAllAppearances());

    // ── Admin ─────────────────────────────────────────────────────────────────

    private void HandleAdminPlayers(HttpListenerContext ctx)
    {
        var stats = _tracker.GetAllStats();
        var online = _sapi.World.AllOnlinePlayers.Select(p => p.PlayerUID).ToHashSet();
        var bans   = _bans.GetBans();

        var result = stats.Select(p => new
        {
            p.PlayerUid,
            p.PlayerName,
            p.PlaytimeSeconds,
            p.Deaths,
            p.PlayerKills,
            p.DistanceWalkedMeters,
            p.FirstSeen,
            p.LastSeen,
            p.TotalXSkillsLevel,
            IsOnline   = online.Contains(p.PlayerUid),
            IsBanned   = bans.Any(b => b.PlayerUid == p.PlayerUid && b.Active),
        });
        SendJson(ctx, 200, result);
    }

    private void HandleAdminChatLog(HttpListenerContext ctx)
    {
        var uid = ctx.Request.QueryString["uid"] ?? "";
        var messages = _chat.GetMessages(uid);
        SendJson(ctx, 200, new ChatLogResponse { PlayerUid = uid, Messages = messages });
    }

    private void HandleAdminBans(HttpListenerContext ctx)
    {
        var uid = ctx.Request.QueryString["uid"];
        SendJson(ctx, 200, new BanListResponse { Bans = _bans.GetBans(uid) });
    }

    private void HandleAdminBan(HttpListenerContext ctx)
    {
        var req = ReadBody<AdminActionRequest>(ctx);
        if (req == null) { SendJson(ctx, 400, new AdminActionResponse { Success = false, Message = "Invalid body" }); return; }
        SendJson(ctx, 200, _bans.BanPlayer(req));
    }

    private void HandleAdminUnban(HttpListenerContext ctx)
    {
        var req = ReadBody<AdminActionRequest>(ctx);
        if (req == null) { SendJson(ctx, 400, new AdminActionResponse { Success = false, Message = "Invalid body" }); return; }
        SendJson(ctx, 200, _bans.UnbanPlayer(req.PlayerUid));
    }

    private void HandleAdminKick(HttpListenerContext ctx)
    {
        var req = ReadBody<AdminActionRequest>(ctx);
        if (req == null) { SendJson(ctx, 400, new AdminActionResponse { Success = false, Message = "Invalid body" }); return; }
        SendJson(ctx, 200, _bans.KickPlayer(req));
    }

    private void HandleAdminWhitelist(HttpListenerContext ctx)
        => SendJson(ctx, 200, new WhitelistResponse { Entries = _whitelist.GetAll() });

    private void HandleAdminWhitelistAdd(HttpListenerContext ctx)
    {
        var req = ReadBody<AdminActionRequest>(ctx);
        if (req == null) { SendJson(ctx, 400, new AdminActionResponse { Success = false, Message = "Invalid body" }); return; }
        SendJson(ctx, 200, _whitelist.AddPlayer(req.PlayerUid, req.PlayerName));
    }

    private void HandleAdminWhitelistRemove(HttpListenerContext ctx)
    {
        var uid = ctx.Request.QueryString["uid"] ?? "";
        SendJson(ctx, 200, _whitelist.RemovePlayer(uid));
    }

    private void HandleAdminImportChat(HttpListenerContext ctx)
    {
        try
        {
            using var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
            var body = reader.ReadToEnd();
            var messages = JsonSerializer.Deserialize<List<ChatMessage>>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (messages == null) { SendJson(ctx, 400, new AdminActionResponse { Success = false, Message = "Invalid body" }); return; }
            _chat.ImportMessages(messages);
            SendJson(ctx, 200, new AdminActionResponse { Success = true, Message = $"Importováno {messages.Count} zpráv." });
        }
        catch (Exception ex)
        {
            SendJson(ctx, 400, new AdminActionResponse { Success = false, Message = ex.Message });
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SendJson(HttpListenerContext ctx, int status, object data)
    {
        var json  = JsonSerializer.Serialize(data, _jsonOpts);
        var bytes = Encoding.UTF8.GetBytes(json);
        ctx.Response.StatusCode      = status;
        ctx.Response.ContentType     = "application/json";
        ctx.Response.ContentLength64 = bytes.Length;
        ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");
        ctx.Response.OutputStream.Write(bytes);
    }

    private T? ReadBody<T>(HttpListenerContext ctx)
    {
        try
        {
            using var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
            return JsonSerializer.Deserialize<T>(reader.ReadToEnd(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch { return default; }
    }

    public override void Dispose()
    {
        _cts?.Cancel();
        _listener?.Stop();
        _tracker?.Dispose();
        _chat?.Dispose();
        _bans?.Dispose();
        _whitelist?.Dispose();
        base.Dispose();
    }
}
