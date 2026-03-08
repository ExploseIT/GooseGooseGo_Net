

using GGG_WorkerService;
using GooseGooseGo_Net.ef;
using GooseGooseGo_Net.Services;
using GooseGooseGo_Net.svc_mexc;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<dbContext>(options => options.UseSqlServer(connectionString));
        services.AddSingleton<IConfiguration>(configuration);

        // 🔹 Mexc cache + WS
        services.AddSingleton<IPriceCache, PriceCache>(); //services.AddSingleton<PriceCache, PriceCache>();
        //services.AddHostedService<MexcWsPriceStreamService>();
        services.AddHostedService<MexcFuturesWsService>();           // ✅ keep this

        services.AddSingleton<AssetDataService>();
        services.AddSingleton<PriceCache, PriceCache>();
        services.AddHostedService<Worker>();
        services.AddHttpClient();

        services.AddSingleton<HubConnection>(sp =>
        {
            var hubUrl = configuration["SignalR:HubUrl"] ?? "https://your-web-app/pricesHub";
            return new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect()
                .Build();
        });
    })
    .UseWindowsService()
    .Build();

host.Run();
