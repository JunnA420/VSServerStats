using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace VSServerStats.Web.Services;

/// <summary>
/// Bridges ASP.NET Core cookie authentication into Blazor Server's AuthenticationStateProvider.
/// </summary>
public class CookieAuthStateProvider : AuthenticationStateProvider
{
    private readonly IHttpContextAccessor _http;

    public CookieAuthStateProvider(IHttpContextAccessor http) => _http = http;

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var user = _http.HttpContext?.User ?? new ClaimsPrincipal(new ClaimsIdentity());
        return Task.FromResult(new AuthenticationState(user));
    }
}
