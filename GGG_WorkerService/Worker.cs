
using GooseGooseGo_Net.Services;

namespace GGG_WorkerService
{
    public class Worker : BackgroundService
    {
        private readonly AssetDataService _assetDataService;
        private readonly IConfiguration _conf;

        public Worker(AssetDataService assetDataService, IConfiguration conf)
        {
            _assetDataService = assetDataService;
            _conf = conf;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var section = _conf.GetSection("AssetDataSettings");
            var fetchInterval = section.GetValue<int>("AssetDataRetrievalRateInSeconds", 60);
            while (!stoppingToken.IsCancellationRequested)
            {
                await _assetDataService.RetrieveAndStoreAssetDataAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(fetchInterval), stoppingToken);
            }
        }
    }
}
