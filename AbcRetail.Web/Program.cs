using AbcRetail.Infrastructure;
using AbcRetail.Core.Interfaces;
using AbcRetail.Core.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
// Simple cookie authentication & authorization
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme;
}).AddCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});
builder.Services.AddAuthorization();
builder.Services.AddStorageServices(builder.Configuration);

var app = builder.Build();

// Write a test entry to File Share logs at startup (one-time)
try
{
    using var scope = app.Services.CreateScope();
    var appLogger = scope.ServiceProvider.GetService<IAppLogger>();
    if (appLogger != null)
    {
        appLogger.LogInfoAsync("WebApp Startup: test log entry to verify file share connectivity").GetAwaiter().GetResult();
    }
}
catch { /* swallow - diagnostics only */ }

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // serve wwwroot
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
