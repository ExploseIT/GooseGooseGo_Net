
using GooseGooseGo_Net.ef;
using GooseGooseGo_Net.Models;
using GooseGooseGo_Net.Services;
using Microsoft.EntityFrameworkCore;

using Microsoft.AspNetCore.RateLimiting;
using System.Text.Json;


var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var cookieAuthOptions = new CookieAuthOptions();

builder.Services.AddRateLimiter(_ => _.AddFixedWindowLimiter("tv", o => {
    o.PermitLimit = 30;        // 30 req / 10s is plenty
    o.Window = TimeSpan.FromSeconds(10);
    o.QueueLimit = 100;
}));

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Configuration.GetSection("CookieAuth").Bind(cookieAuthOptions);
builder.Services.AddDbContext<dbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddSingleton<AssetDataService>();
builder.Services.AddAuthorization();
builder.Services.AddControllersWithViews(); // <-- Use this for MVC views
//builder.Services.AddSingleton<mApp>();
builder.Services.AddHttpClient();
// builder.Services.AddRazorPages(); // <-- Add if you use Razor Pages

var app = builder.Build();

app.UseRateLimiter();

// Initialise from config
// read from config (appsettings, env vars, user-secrets, etc.)
var sharedSecret = builder.Configuration.GetValue<string>("WEBHOOK:WEBHOOK_SECRET")
                   ?? throw new InvalidOperationException("WEBHOOK:WEBHOOK_SECRET missing");

// before MapPost(...)
app.UsePathBase("/goosegoosego");

app.MapPost("/v1/signal_info", async (HttpRequest req, HttpResponse res) =>
{
    using var doc = await JsonDocument.ParseAsync(req.Body);
    var root = doc.RootElement;

    // 1) basic validation
    if (!root.TryGetProperty("secret", out var secretEl) ||
        !string.Equals(secretEl.GetString(), sharedSecret, StringComparison.Ordinal))
    {
        res.StatusCode = StatusCodes.Status401Unauthorized;
        await res.WriteAsJsonAsync(new { ok = false, error = "bad_secret" });
        return;
    }

    var sip = new cSignalInfoPost();

    try
    { 
    // 2) extract fields cared about
        sip.sip_symbol = root.TryGetProperty("symbol", out var sym) ? sym.GetString() : "";  //root.GetProperty("symbol").GetString();
        sip.sip_strategy = root.TryGetProperty("strategy", out var strat) ? strat.GetString() : "";
        sip.sip_action = root.TryGetProperty("action", out var a) ? a.GetString() : "";
        sip.sip_price = root.TryGetProperty("price", out var p) ? p.GetString() : "";
        sip.sip_whenStr = root.TryGetProperty("time", out var t) ? t.GetString() : "";
    }
    catch (Exception ex)
    {
        res.StatusCode = StatusCodes.Status400BadRequest;
        await res.WriteAsJsonAsync(new { ok = false, error = "invalid_payload", details = ex.Message });
        return;
    }

    // 3) hand off to background work (don’t block the webhook)
    _ = Task.Run(() =>
    {
        // TODO: enqueue to your bus/queue, save to DB, trigger bot, etc.
        Console.WriteLine($"TV: {sip.sip_symbol} {sip.sip_strategy} {sip.sip_action} @ {sip.sip_price} time={sip.sip_whenStr}");
    });

    // 4) ack quickly
    await res.WriteAsJsonAsync(new { ok = true });
})
.RequireRateLimiting("tv");

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
