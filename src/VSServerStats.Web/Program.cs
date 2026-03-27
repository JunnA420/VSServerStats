using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using VSServerStats.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddHttpClient<StatsService>();
builder.Services.AddHttpClient<AdminService>();
builder.Services.AddScoped<IFileService, FileService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/admin/login";
        options.LogoutPath = "/admin/logout";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.Cookie.Name = "vsadmin";
    });

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider,
    VSServerStats.Web.Services.CookieAuthStateProvider>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Login POST endpoint
app.MapPost("/admin/login", async (HttpContext ctx, IConfiguration config) =>
{
    var form = await ctx.Request.ReadFormAsync();
    var password = form["password"].ToString();
    var expected = config["Dashboard:AdminPassword"] ?? "";

    if (password == expected)
    {
        var claims = new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "admin") };
        var identity = new System.Security.Claims.ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new System.Security.Claims.ClaimsPrincipal(identity));
        ctx.Response.Redirect("/admin");
    }
    else
    {
        ctx.Response.Redirect("/admin/login?error=1");
    }
});

// Logout endpoint
app.MapPost("/admin/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    ctx.Response.Redirect("/admin/login");
});

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
