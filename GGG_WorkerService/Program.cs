

using GGG_WorkerService;
using GooseGooseGo_Net.Services;
using GooseGooseGo_Net.ef;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<dbContext>(options => options.UseSqlServer(connectionString));
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<AssetDataService>();
        services.AddHostedService<Worker>();
        services.AddHttpClient();
    })
    .UseWindowsService()
    .Build();

host.Run();
