using GooseGooseGo_Net.ef;
using GooseGooseGo_Net.Services;
using Microsoft.EntityFrameworkCore;

namespace GooseGooseGo_Net.Services
{
    public class AssetDataService
    {
        private readonly IServiceProvider _services;
        private readonly IConfiguration _conf;

        public AssetDataService(IServiceProvider services, IConfiguration conf)
        {
            _services = services;
            _conf = conf;
        }

        public async Task RetrieveAndStoreAssetDataAsync(CancellationToken stoppingToken)
        {
            Exception? exc = null;
            using (var scope = _services.CreateScope())
            {
                // Retrieve asset data from Kraken and Crypto.com
                // Store in DB using your DbContext
                var dbCon = scope.ServiceProvider.GetRequiredService<dbContext>();

                var resp = await KrakenClient.Request(
                    method: "GET",
                    path: "/0/public/Ticker",
                    conf: _conf,
                    environment: "https://api.kraken.com"
                );

                var stream = await resp.Content.ReadAsStreamAsync();
                KrakenEnvelope<Dictionary<string, KrakenTickerEntry>>? krakenData = await System.Text.Json.JsonSerializer.DeserializeAsync<
                    KrakenEnvelope<Dictionary<string, KrakenTickerEntry>>
                >(stream);


                var all = await CryptoComClient.GetTickersAsync(_conf);

                var db = scope.ServiceProvider.GetRequiredService<dbContext>();
                var now = DateTime.UtcNow;
                cKrakenAssetInfo kai = new cKrakenAssetInfo();
                var e_kraken = new ent_kraken(dbCon);
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
            }
        }
    }

}
