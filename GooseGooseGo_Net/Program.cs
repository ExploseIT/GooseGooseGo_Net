using GooseGooseGo_Net.ef;
using GooseGooseGo_Net.Models;
using GooseGooseGo_Net.Services;
using Microsoft.EntityFrameworkCore;


var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var cookieAuthOptions = new CookieAuthOptions();

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
