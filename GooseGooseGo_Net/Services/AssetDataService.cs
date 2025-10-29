
using GooseGooseGo_Net.ef;
using GooseGooseGo_Net.Models;
using GooseGooseGo_Net.Services;
using Microsoft.EntityFrameworkCore;

namespace GooseGooseGo_Net.Services
{
    public class AssetDataService
    {
        private readonly IServiceProvider _services;
        private readonly IConfiguration _conf;
        private readonly ILogger<mApp> _logger;
        private IHttpClientFactory _httpClientFactory;

        public AssetDataService(IServiceProvider services, IConfiguration conf, ILogger<mApp> logger, IHttpClientFactory httpClientFactory)
        {
            _services = services;
            _conf = conf;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        public async Task RetrieveAndStoreAssetDataAsync(CancellationToken stoppingToken)
        {
            Exception? exc = null;
            using (var scope = _services.CreateScope())
            {
                // Retrieve asset data 
                // Store in DB using your DbContext
                var _conf = scope.ServiceProvider.GetRequiredService<IConfiguration>();
                var _dbCon = scope.ServiceProvider.GetRequiredService<dbContext>();
                List<KeyValuePair<string, string>>? qListIn = null;
                var e_asset = new ent_asset(_conf, _logger, _httpClientFactory);
                var e_kraken = new ent_kraken(_conf, _logger, _httpClientFactory,_dbCon);
                var e_mexc = new ent_mexc(_conf, _logger, _httpClientFactory, _dbCon);
                //var e_cryptocom = new ent_cryptocom(_conf, _logger, _httpClientFactory, _dbCon);

                //var cryptocomData = await e_cryptocom.doApi_TickerListAsync(_dbCon);
                var mexcData = await e_mexc.doApi_TickerListAsync(_dbCon!, qListIn, stoppingToken);

                var krakenData = await e_kraken.doApi_TickerListAsync(_dbCon);
                

                var now = DateTime.UtcNow;
                cAssetAssetInfo assassInfo = new cAssetAssetInfo();
                
                try
                {
                    if (krakenData?.Result != null)
                    {
                        assassInfo = e_asset.doAssetGetNextId(_dbCon)!;

                        foreach (var kvp in krakenData.Result)
                        {
                            var entry = kvp.Value;
                            decimal c_dec = 0.0M;
                            if (kvp.Key.EndsWith("USD", StringComparison.OrdinalIgnoreCase))
                            {
                                var asset = new cAsset
                                {
                                    assId = 0, // Assuming 0 means new entry; adjust as needed
                                    assIndex = assassInfo.asiId,
                                    assExchange = "exc_kraken",
                                    assPair = kvp.Key,
                                    assLastTrade = entry.LastTrade != null && entry.LastTrade.Length > 0 ? decimal.Parse(entry.LastTrade[0]) : 0,
                                    assOpen = entry.Open != null ? decimal.Parse(entry.Open) : (decimal?)null,
                                    assBid = entry.Bid != null && entry.Bid.Length > 0 ? decimal.Parse(entry.Bid[0]) : (decimal?)null,
                                    assAsk = entry.Ask != null && entry.Ask.Length > 0 ? decimal.Parse(entry.Ask[0]) : (decimal?)null,
                                    assHigh24h = entry.High != null && entry.High.Length > 1 ? decimal.Parse(entry.High[1]) : (decimal?)null,
                                    assLow24h = entry.Low != null && entry.Low.Length > 1 ? decimal.Parse(entry.Low[1]) : (decimal?)null,
                                    assVolume24h = decimal.TryParse(entry.Volume![1], out c_dec) ? c_dec: null,
                                    assRetrievedAt = now

                                };
                                e_asset.doAssetUpdateById(_dbCon, asset);
                            }
                        }
                        foreach (var assd in mexcData!)
                        {
                            decimal c_dec = 0.0M;
                            if (assd.symbol.EndsWith("USDT", StringComparison.OrdinalIgnoreCase) ||
                                assd.symbol.EndsWith("USDC", StringComparison.OrdinalIgnoreCase))
                            {
                                var asset = new cAsset
                                {
                                    assId = 0, // Assuming 0 means new entry; adjust as needed
                                    assIndex = assassInfo.asiId,
                                    assExchange = "exc_mexc",
                                    assPair = assd.symbol,
                                    assLastTrade = assd.lastPrice != null && assd.lastPrice.Length > 0 ? decimal.Parse(assd.lastPrice) : 0,
                                    assOpen = assd.openPrice != null ? decimal.Parse(assd.openPrice) : (decimal?)null,
                                    assBid = assd.bidPrice != null && assd.bidPrice.Length > 0 ? decimal.Parse(assd.bidPrice!) : (decimal?)null,
                                    assAsk = assd.askPrice != null && assd.askPrice.Length > 0 ? decimal.Parse(assd.askPrice) : (decimal?)null,
                                    assHigh24h = assd.highPrice != null && assd.highPrice.Length > 1 ? decimal.Parse(assd.highPrice) : (decimal?)null,
                                    assLow24h = decimal.Parse("0.0"),
                                    assVolume24h = decimal.TryParse(assd.volume, out c_dec) ? c_dec : null,
                                    assRetrievedAt = now

                                };
                                e_asset.doAssetUpdateById(_dbCon, asset);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    exc = e;
                }

                var p_kps = new cAssetPercentageSwingParms
                {
                    aspspMinSwing = 0.010M,
                    aspspPeriodValue = 5,
                    aspspPeriodUnit = "minute",
                    aspspPeriodOffset = 0
                };

                var clkps = e_asset.doAssetPercentageSwingList(_dbCon, p_kps);
            }

        }
    }

}
