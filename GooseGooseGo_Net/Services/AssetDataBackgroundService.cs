using GooseGooseGo_Net.ef;
using GooseGooseGo_Net.Services;
using GooseGooseGo_Net.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace GooseGooseGo_Net.Services
{
    public class AssetDataBackgroundService : BackgroundService
    {
        private readonly AssetDataService _assetDataService;

        public AssetDataBackgroundService(AssetDataService assetDataService)
        {
            _assetDataService = assetDataService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await _assetDataService.RetrieveAndStoreAssetDataAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
}



