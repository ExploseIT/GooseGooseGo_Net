using GooseGooseGo_Net.ef;
using GooseGooseGo_Net.Services;
using Microsoft.EntityFrameworkCore;

namespace GooseGooseGo_Net.Services
{
    public class AssetDataService
    {
        private readonly IServiceProvider _services;
        private readonly IConfiguration _conf;
        private readonly ILogger<AssetDataService>? _logger;

        public AssetDataService(IServiceProvider services, IConfiguration conf, ILogger<AssetDataService>? logger)
        {
            _services = services;
            _conf = conf;
            _logger = logger;
        }

        public async Task RetrieveAndStoreAssetDataAsync(CancellationToken stoppingToken)
        {
            Exception? exc = null;
            using (var scope = _services.CreateScope())
            {
                // Retrieve asset data 
                // Store in DB using your DbContext
                var _conf = scope.ServiceProvider.GetRequiredService<IConfiguration>();
                var dbCon = scope.ServiceProvider.GetRequiredService<dbContext>();

                var e_kraken = new ent_kraken(_conf, dbCon, _logger!);
                var krakenData = await e_kraken.doApi_TickerListAsync();
                //var all = await CryptoComClient.GetTickersAsync(_conf);

                var now = DateTime.UtcNow;
                cKrakenAssetInfo kai = new cKrakenAssetInfo();
                
                try
                {
                    if (krakenData?.Result != null)
                    {
                        kai = e_kraken.doKrakenGetNextId();

                        foreach (var kvp in krakenData.Result)
                        {
                            var entry = kvp.Value;
                            var asset = new cKraken
                            {
                                kaId = 0, // Assuming 0 means new entry; adjust as needed
                                kaIndex = kai.kaiId,
                                kaPair = kvp.Key,
                                kaLastTrade = entry.LastTrade != null && entry.LastTrade.Length > 0 ? decimal.Parse(entry.LastTrade[0]) : 0,
                                kaOpen = entry.Open != null ? decimal.Parse(entry.Open) : (decimal?)null,
                                kaBid = entry.Bid != null && entry.Bid.Length > 0 ? decimal.Parse(entry.Bid[0]) : (decimal?)null,
                                kaAsk = entry.Ask != null && entry.Ask.Length > 0 ? decimal.Parse(entry.Ask[0]) : (decimal?)null,
                                kaHigh24h = entry.High != null && entry.High.Length > 1 ? decimal.Parse(entry.High[1]) : (decimal?)null,
                                kaLow24h = entry.Low != null && entry.Low.Length > 1 ? decimal.Parse(entry.Low[1]) : (decimal?)null,
                                kaVolume24h = entry.Volume != null && entry.Volume.Length > 1 ? entry.Volume[1] : null,
                                kaRetrievedAt = now

                            };
                            if (asset.kaPair.EndsWith("USD", StringComparison.OrdinalIgnoreCase))
                            {
                                e_kraken.doKrakenUpdateById(asset);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    exc = e;
                }

                var p_kps = new cKrakenPercentageSwingParms
                {
                    kapsMinSwing = 0.010M,
                    kapsPeriodValue = 5,
                    kapsPeriodUnit = "minute",
                    kapsPeriodOffset = 0
                };

                var clkps = e_kraken.doKrakenPercentageSwingList(p_kps);
            }

        }
    }

}
