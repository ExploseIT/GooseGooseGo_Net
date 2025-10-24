
using GooseGooseGo_Net.ef;
using GooseGooseGo_Net.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static GooseGooseGo_Net.ef.ent_asset;

namespace GooseGooseGo_Net.ef
{
    /// <summary>
    /// Singleton-safe Kraken logic. No per-instance mutable state, DI for singleton-safe services.
    /// Pass dbContext as method argument for DB operations.
    /// Uses IHttpClientFactory for HTTP calls.
    /// </summary>
    public class ent_kraken : cIsDbNull
    {
        private readonly IConfiguration _conf;
        private readonly ILogger<mApp> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _encryptionKey;
        private readonly cApiDetails? _apiDetails;

        Exception? exc = null;
        private static readonly SemaphoreSlim _krakenGate = new(1, 1);

        public ent_kraken(
            IConfiguration conf,
            ILogger<mApp> logger,
            IHttpClientFactory httpClientFactory,
            dbContext _dbCon
            )
        {
            _conf = conf;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _encryptionKey = conf.GetSection("Encryption").GetValue<string>("EncryptionKey") ?? "";
            _apiDetails = doApiDetailsDecrypt(_dbCon!)!;
        }

        public string doApiDetailsEncrypt(dbContext dbCon)
        {
            string ret = "";
            var _e_settings = new ent_setting(dbCon, _logger);
            var set_kraken_api = _e_settings.doSettingsReadByName("KRAKEN_API");
            if (set_kraken_api == null)
            {
                var cs = _conf.GetSection("KRAKEN_API");
                string _apiUrl = cs.GetValue<string>("KRAKEN_API_URL") ?? "";
                string _apiKey = cs.GetValue<string>("KRAKEN_KEY") ?? "";
                string _apiSecret = cs.GetValue<string>("KRAKEN_SECRET") ?? "";

                var cApiDetails = new cApiDetails
                {
                    apidet_api_url = _apiUrl,
                    apidet_key = _apiKey,
                    apidet_secret = _apiSecret
                };
                string json = JsonSerializer.Serialize(cApiDetails);
                ret = mEncryption.EncryptString(json, _encryptionKey);
                _e_settings.doSettingsInsertByName("KRAKEN_API", ret, "Encrypted Kraken API details");
            }
            return ret;
        }

        public cApiDetails? doApiDetailsDecrypt(dbContext dbCon)
        {
            cApiDetails? ret = null;
            try
            {
                string api_index = "KRAKEN_API";
                var _e_settings = new ent_setting(dbCon, _logger);
                c_setting? set_mexc_api = _e_settings.doSettingsReadByName(api_index);
                if (set_mexc_api != null)
                {
                    var retJson = mEncryption.DecryptString(set_mexc_api.setValue, _encryptionKey);
                    ret = JsonSerializer.Deserialize<cApiDetails>(retJson)!;
                }
                else
                {
                    var _r_enc = doApiDetailsEncrypt(dbCon);
                    set_mexc_api = _e_settings.doSettingsReadByName(api_index);
                    if (set_mexc_api != null)
                    {
                        var retJson = mEncryption.DecryptString(set_mexc_api.setValue, _encryptionKey);
                        ret = JsonSerializer.Deserialize<cApiDetails>(retJson)!;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decrypting Kraken API details");
            }
            return ret;
        }

        public async Task<ApiResponse<List<cKrakenPortfolio>>?> doKrakenReturnPortfolio(dbContext _dbCon, CancellationToken ct=default)
        {
            ApiResponse<List<cKrakenPortfolio>> ret = new ApiResponse<List<cKrakenPortfolio>>();
            try
            {
                //var _e_kraken = new ent_kraken(_conf, _logger, _httpClientFactory, _dbCon);
                // Get Kraken Portfolio Data
                KrakenEnvelope<KrakenTradesHistoryResult>? krakenTradesHistoryData = await doApi_TradesHistoryAsync(_dbCon, ct);
                var kbdResp = await doApi_AssetBalanceAsync(_dbCon, ct);
                if (!kbdResp!.apiSuccess)
                {
                    throw new InvalidOperationException($"Kraken doApi_AssetBalanceAsync call failed: {kbdResp.apiMessage}");
                }
                Dictionary<string, decimal>? krakenBalanceData = kbdResp.apiData;
                var _krakenTickerParms = doGetTickerPairsFromBalance(krakenBalanceData);
                KrakenEnvelope<Dictionary<string, KrakenTickerEntry>>? krakenTickerData = await doApi_TickerAsync(_dbCon, _krakenTickerParms, ct);
                var _apiResp = doGetPortfolio(_dbCon, krakenTradesHistoryData, krakenBalanceData, krakenTickerData);
                if(!_apiResp.apiSuccess)
                {
                    throw new InvalidOperationException($"Kraken doGetPortfolio call failed: {_apiResp.apiMessage}");
                }
                ret.apiData = _apiResp.apiData;
                ret.apiSuccess = true;
            }
            catch (Exception ex)
            {
                ret.apiSuccess = false;
                ret.apiMessage = ex.Message;
            }
            return ret;
        }

        public ApiResponse<List<cKrakenPortfolio>> doGetPortfolio(dbContext _dbCon, KrakenEnvelope<KrakenTradesHistoryResult>? krakenTradesHistoryData, 
            Dictionary<string, decimal>? krakenBalanceData,
            KrakenEnvelope<Dictionary<string, KrakenTickerEntry>>? krakenTickerData)
        {
            ApiResponse<List<cKrakenPortfolio>> ret = new ApiResponse<List<cKrakenPortfolio>>();
            
            ret.apiData = new List<cKrakenPortfolio>();

            decimal totalUsdValue = 0m;
            decimal totalUnrealizedPnl = 0m;
            try
            {
                foreach (var bal in krakenBalanceData!) // Dictionary<string, decimal> e.g. { "SPICE": 123.45m }
                {
                    var asset = bal.Key;
                    var qtyHeld = bal.Value;
                    if (qtyHeld <= 0) continue;


                    var pair = asset + "USD"; // adjust if you prefer another quote

                    // Build a lookup so we can quickly get trades for "SPICEUSD", "BABYUSD", etc.
                    var tradesByPair = krakenTradesHistoryData?.Result?.Trades?
                        .Values
                        .Where(t => !string.IsNullOrEmpty(t.Pair))
                        .GroupBy(t => t.Pair!)
                        .ToDictionary(g => g.Key, g => g.AsEnumerable(), StringComparer.OrdinalIgnoreCase)
                        ?? new Dictionary<string, IEnumerable<KrakenTrade>>(StringComparer.OrdinalIgnoreCase);

                    // For quick last prices:
                    var lastPriceByPair = krakenTickerData?.Result?
                        .ToDictionary(
                            kv => kv.Key,
                            kv => decimal.TryParse(kv.Value.LastTrade?.FirstOrDefault(),
                                                   NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m,
                            StringComparer.OrdinalIgnoreCase
                        ) ?? new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);


                    if (!lastPriceByPair.TryGetValue(pair, out var lastPrice) || lastPrice <= 0)
                        continue;

                    tradesByPair.TryGetValue(pair, out var tradesForPair);
                    var pos = tradesForPair is null ? new Position { Qty = qtyHeld, AvgCost = 0m } : BuildPosition(tradesForPair);

                    // If positions differ slightly from balance (deposits/airdrops), trust the balance
                    var avgCost = pos.Qty > 0 ? pos.AvgCost : pos.AvgCost; // pos.AvgCost is our best available cost basis
                    var marketValue = qtyHeld * lastPrice;
                    var unrealized = (avgCost > 0 ? (lastPrice - avgCost) * qtyHeld : 0m);

                    totalUsdValue += marketValue;
                    totalUnrealizedPnl += unrealized;

                    var _mKrakenPortfolio = new cKrakenPortfolio
                    {
                        kpAsset = asset,
                        kpQtyHeld = qtyHeld,
                        kpAvgCost = avgCost,
                        kpLastPrice = lastPrice,
                        kpMarketValue = marketValue,
                        kpUnrealizedPnl = unrealized,
                        kpRealizedPnl = pos.RealizedPnl,
                        kpFeesPaid = pos.FeesPaid,
                        kpRetrievedAt = DateTime.UtcNow
                    };

                    ret.apiData.Add(_mKrakenPortfolio);

                }

                var value_total = $"\nTotal Value  ≈ {totalUsdValue:N2} USD";
                var value_unrealised = $"Unrealized P/L ≈ {totalUnrealizedPnl:N2} USD";
            }
            catch (Exception ex)
            {
                ret.apiSuccess = false;
                ret.apiMessage = ex.Message;
            }
            return ret;
        }



// Turns Kraken trades (for one pair) into a Position (avg cost etc.)
public static Position BuildPosition(IEnumerable<KrakenTrade> trades)
        {
            var pos = new Position();
            var ci = CultureInfo.InvariantCulture;

            foreach (var t in trades.OrderBy(x => x.Time ?? 0.0))
            {
                var side = t.Type;  // "buy" or "sell"
                var price = t.Price ?? 0m;         // quote per unit
                var qty = t.Volume ?? 0m;        // base units
                var fee = t.Fee ?? 0m;           // quote currency (assumed)

                if (qty <= 0 || price <= 0) continue;

                if (string.Equals(side, "buy", StringComparison.OrdinalIgnoreCase))
                {
                    // New total cost = existing position cost + (trade cost + fee)
                    var existingCost = pos.AvgCost * pos.Qty;
                    var tradeCost = price * qty + fee;
                    var newQty = pos.Qty + qty;

                    pos.AvgCost = newQty > 0 ? (existingCost + tradeCost) / newQty : 0m;
                    pos.Qty = newQty;
                    pos.FeesPaid += fee;
                }
                else if (string.Equals(side, "sell", StringComparison.OrdinalIgnoreCase))
                {
                    // Realized P&L on the portion sold, net of fee
                    var pnl = (price - pos.AvgCost) * qty - fee;
                    pos.RealizedPnl += pnl;
                    pos.Qty -= qty;
                    if (pos.Qty < 0) pos.Qty = 0;   // defensive
                    pos.FeesPaid += fee;

                    // AvgCost stays the same in weighted-average method when selling
                    if (pos.Qty == 0) pos.AvgCost = 0m; // closed out
                }
            }

            return pos;
        }


        public async Task<KrakenEnvelope<Dictionary<string, KrakenTickerEntry>>?> doApi_TickerListAsync(dbContext _dbCon)
        {
            Exception? exc = null;
            string retJson = "";
            KrakenEnvelope<Dictionary<string, KrakenTickerEntry>>? ret = null;
            ApiResponse<string>? ret_ApiBase = null;
            try
            {
                cApiParms p = new cApiParms
                {
                    apMethod = "GET",
                    apPath = "/0/public/Ticker",
                    apDoSign = false
                };

                ret_ApiBase = await doApi_Base<object>(p, _dbCon);

                if (!ret_ApiBase.apiSuccess)
                    throw new InvalidOperationException($"Kraken API call failed: {ret_ApiBase.apiMessage}");

                retJson = ret_ApiBase.apiData!;

                ret = JsonSerializer.Deserialize<KrakenEnvelope<Dictionary<string, KrakenTickerEntry>>?>(retJson);
            }
            catch (Exception ex)
            {
                exc = ex;
            }
            return ret;
        }

        public async Task<ApiResponse<Dictionary<string, decimal>>?> doApi_AssetBalanceAsync(dbContext _dbCon, CancellationToken ct = default)
        {
            ApiResponse<Dictionary<string, decimal>> ret = new ApiResponse<Dictionary<string, decimal>>();
            ret.apiData = new Dictionary<string, decimal>();

            ApiResponse<string>? ret_ApiBase = null;
            string retJson = "";
            cApiParms p = new cApiParms
            {
                apMethod = "POST",
                apPath = "/0/private/Balance",
                apDoSign = true
            };
            try
            {

                ret_ApiBase = await doApi_Base<object>(p, _dbCon);
                if (!ret_ApiBase.apiSuccess)
                    throw new InvalidOperationException($"Kraken API call failed: {ret_ApiBase.apiMessage}");
                retJson = ret_ApiBase.apiData!;

                decimal minAmount = 0.01m;
                bool excludeFundingSuffixF = true;
                {
                    var env = JsonSerializer.Deserialize<KrakenEnvelope<Dictionary<string, string>>>(retJson)
                              ?? throw new InvalidOperationException("Empty Kraken response.");

                    if (env.Error?.Count > 0) throw new InvalidOperationException(string.Join("; ", env.Error));

                    var ci = CultureInfo.InvariantCulture;

                    ret.apiData = (env.Result ?? new Dictionary<string, string>())
                        .Select(kv => new
                        {
                            Asset = kv.Key,
                            Amount = decimal.TryParse(kv.Value, NumberStyles.Any, ci, out var d) ? d : 0m
                        })
                        .Where(x =>
                            x.Amount > minAmount &&
                            (!excludeFundingSuffixF || !x.Asset.EndsWith(".F", StringComparison.OrdinalIgnoreCase)))
                        .ToDictionary(x => x.Asset, x => x.Amount);
                }
            }
            catch (Exception ex)
            {
                exc = ex;
                ret.apiSuccess = false;
                ret.apiMessage = ex.Message;
            }
            return ret;
        }



        public async Task<KrakenEnvelope<KrakenTradesHistoryResult>?> doApi_TradesHistoryAsync(dbContext _dbCon, CancellationToken ct=default)
        {
            cApiParms p = new cApiParms
            {
                apMethod = "POST",
                apPath = "/0/private/TradesHistory",
                apDoSign = true
            };
            
            KrakenEnvelope<KrakenTradesHistoryResult>? ret = null;
            string retJson = "";
            ApiResponse<string>? ret_ApiBase = null;
            try
            {
                ret_ApiBase = await doApi_Base<object>(p, _dbCon);
                if (!ret_ApiBase.apiSuccess)
                    throw new InvalidOperationException($"Kraken API call failed: {ret_ApiBase.apiMessage}");
                retJson = ret_ApiBase.apiData!;

                ret = JsonSerializer.Deserialize<KrakenEnvelope<KrakenTradesHistoryResult>?>(retJson);
            }
            catch (Exception ex)
            {
                exc = ex;
            }
            return ret;
        }

        public async Task<KrakenEnvelope<Dictionary<string, KrakenTickerEntry>>?> doApi_TickerAsync(dbContext _dbCon, KrakenTickerParams parms, CancellationToken ct)
        {
            Exception? exc = null;
            KrakenEnvelope<Dictionary<string, KrakenTickerEntry>>? ret = null;
            string retJson = "";
            ApiResponse<string>? ret_ApiBase = null;
            try
            { 
                cApiParms p = new cApiParms
                {
                    apMethod = "GET",
                    apPath = "/0/public/Ticker",
                    apDoSign = true
                };

                ret_ApiBase = await doApi_Base<object>(p, _dbCon);
                if (!ret_ApiBase.apiSuccess)
                    throw new InvalidOperationException($"Kraken API call failed: {ret_ApiBase.apiMessage}");
                retJson = ret_ApiBase.apiData!;

                ret = JsonSerializer.Deserialize<KrakenEnvelope<Dictionary<string, KrakenTickerEntry>>?>(retJson);
            }
            catch (Exception ex)
            {
                exc = ex;
            }

            return ret;
        }

        public async Task<ApiResponse<string>> doApi_Base<T>(cApiParms p, dbContext _dbCon, T? parms = default, CancellationToken ct = default)
    where T : class
        {
            ApiResponse<string> ret = new ApiResponse<string>();
            ret.apiData = "";

            try
            {
                var environment = _apiDetails!.apidet_api_url!.TrimEnd('/');
                var url = environment + p.apPath;

                // Nonce is required for private calls
                //var nonce = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                // Nonce is required for private calls
                var nonce = NextNonce().ToString();  // 👈 replaces ToUnixTimeMilliseconds()

                // Convert parameters (object or dictionary) into key-value pairs
                var form = new List<KeyValuePair<string, string>>
    {
        new("nonce", nonce)
    };

                if (parms != null)
                {
                    // Convert the object’s public properties into key=value pairs
                    foreach (var prop in parms.GetType().GetProperties())
                    {
                        var name = prop.Name;
                        var value = prop.GetValue(parms)?.ToString() ?? "";
                        form.Add(new KeyValuePair<string, string>(name, value));
                    }
                }

                var formBody = new FormUrlEncodedContent(form);
                var postDataStr = await formBody.ReadAsStringAsync();

                // Compute Kraken signature if required
                string sig = "";
                if (p.apDoSign && !string.IsNullOrEmpty(_apiDetails!.apidet_secret))
                    sig = KrakenSign(p.apPath, postDataStr, nonce, _apiDetails!.apidet_secret!);

                // Build the request
                var req = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = formBody
                };

                if (p.apDoSign && !string.IsNullOrEmpty(_apiDetails!.apidet_key))
                {
                    req.Headers.Add("API-Key", _apiDetails!.apidet_key!);
                    req.Headers.Add("API-Sign", sig);
                }

                var client = _httpClientFactory.CreateClient();
                var resp = await client.SendAsync(req);
                

                ret.apiData = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                    throw new HttpRequestException($"{(int)resp.StatusCode} {resp.ReasonPhrase}: {ret.apiData}");

                if (LooksLikeApiError(ret.apiData, out var krakenErrMsg))
                    throw new InvalidOperationException(krakenErrMsg);
            }
            catch (Exception ex)
            {
                exc = ex;
                ret.apiMessage = exc.Message;
                ret.apiSuccess = false;
            }
            return ret;
        }

        private static long _lastNonce = 0;

        private static long NextNonce()
        {
            // microseconds since epoch
            long us = (long)(DateTime.UtcNow - DateTime.UnixEpoch).TotalMilliseconds * 1000L
                      + Random.Shared.Next(0, 1000); // add a bit of jitter within the ms

            while (true)
            {
                long prev = Interlocked.Read(ref _lastNonce);
                long next = us <= prev ? prev + 1 : us;
                if (Interlocked.CompareExchange(ref _lastNonce, next, prev) == prev)
                    return next;
            }
        }

        private static bool LooksLikeApiError(string json, out string message)
        {
            message = "";
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("error", out var arr) &&
                    arr.ValueKind == JsonValueKind.Array)
                {
                    var msgs = arr.EnumerateArray()
                                  .Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() : null)
                                  .Where(s => !string.IsNullOrWhiteSpace(s))
                                  .ToArray();
                    if (msgs.Length > 0)
                    {
                        message = string.Join("; ", msgs!);
                        return true;
                    }
                }
            }
            catch
            {
                // ignore parse errors; treat as not a Kraken error envelope
            }
            return false;
        }

        private static string KrakenSign(string uriPath, string postData, string nonce, string secretB64)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(nonce + postData));

            var toSign = Encoding.UTF8.GetBytes(uriPath).Concat(hash).ToArray();
            using var hmac = new HMACSHA512(Convert.FromBase64String(secretB64));
            var mac = hmac.ComputeHash(toSign);
            return Convert.ToBase64String(mac);
        }


        public KrakenTickerParams doGetTickerPairsFromBalance(Dictionary<string, decimal>? krakenBalanceData)
        {
            KrakenTickerParams? ret = null;

            if (krakenBalanceData == null || krakenBalanceData.Count == 0)
                return new KrakenTickerParams();

            // Kraken usually uses quote codes like USD, EUR, USDT, etc.
            const string quote = "USD";

            // Some Kraken balance keys aren’t valid trading assets (fiat, rewards, .F suffix)
            var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "USD", "EUR", "GBP", "ZUSD", "ZEUR", "ZGBP"
    };

            var pairs = krakenBalanceData
                .Where(kv => kv.Value > 0.00000001m && !excluded.Contains(kv.Key) && !kv.Key.EndsWith(".F", StringComparison.OrdinalIgnoreCase))
                .Select(kv =>
                {
                    // Normalize to Kraken’s format (XBT instead of BTC)
                    var baseSymbol = kv.Key switch
                    {
                        "BTC" => "XBT",
                        _ => kv.Key
                    };
                    return $"{baseSymbol}{quote}";
                })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            ret =  new KrakenTickerParams
            {
                pair = string.Join(",", pairs)
            };

            return ret;
        }

        // --- Model Classes ---

    }

    public class cKrakenPortfolio
    {
        [Key]
        public int kpId { get; set; }
        public string kpAsset { get; set; } = "";
        [Precision(18, 8)]
        public decimal kpQtyHeld { get; set; }
        [Precision(18, 8)]
        public decimal kpAvgCost { get; set; }
        [Precision(18, 8)]
        public decimal kpLastPrice { get; set; }
        [Precision(18, 8)]
        public decimal kpMarketValue { get; set; }
        [Precision(18, 8)]
        public decimal kpUnrealizedPnl { get; set; }
        [Precision(18, 8)]
        public decimal kpRealizedPnl { get; set; }
        [Precision(18, 8)]
        public decimal kpFeesPaid { get; set; }
        public DateTime kpRetrievedAt { get; set; }
        public DateTime kpRetrievedAtSanitised()
        {
            var dt = this.kpRetrievedAt.ToLocalTime();
            return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second);
        }
    }
    public class cKraken
        {
            [Key]
            public int kaId { get; set; }
            public int kaIndex { get; set; }
            public string kaPair { get; set; } = "";
            [Precision(18, 5)]
            public decimal kaLastTrade { get; set; }
            [Precision(18, 5)]
            public decimal? kaOpen { get; set; }
            [Precision(18, 5)]
            public decimal? kaBid { get; set; }
            [Precision(18, 5)]
            public decimal? kaAsk { get; set; }
            [Precision(18, 5)]
            public decimal? kaHigh24h { get; set; }
            [Precision(18, 5)]
            public decimal? kaLow24h { get; set; }
            [Precision(18, 5)]
            public decimal? kaVolume24h { get; set; }
            public DateTime kaRetrievedAt { get; set; }
            public DateTime kaRetrievedAtSanitised()
            {
                var dt = this.kaRetrievedAt.ToLocalTime();
                return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second);
            }
        }


        public class cKrakenInfo
        {
            [Key]
            public string kaPair { get; set; } = "";
            [Precision(18, 5)]
            public decimal kaMinLastTrade { get; set; } = 0.0M;
            [Precision(18, 5)]
            public decimal kaMaxLastTrade { get; set; } = 0.0M;
            [Precision(18, 5)]
            public decimal kaHigh24h { get; set; } = 0.0M;
            [Precision(18, 5)]
            public decimal kaLow24h { get; set; } = 0.0M;
            [Precision(18, 5)]
            public decimal kaVolume24h { get; set; } = 0.0M;
            public DateTime kaRetrievedAt { get; set; } = DateTime.Now;
        }




        public sealed record KrakenEnvelope<T>(
            [property: JsonPropertyName("error")] List<string> Error,
            [property: JsonPropertyName("result")] T? Result
        );

    // ---- /0/private/TradesHistory result ----
    public sealed class KrakenTradesHistoryResult
    {
        [JsonPropertyName("count")] public int Count { get; set; }

        // key = trade id like "TH4B5F-BYCUB-DYBL2J"
        [JsonPropertyName("trades")] public Dictionary<string, KrakenTrade> Trades { get; set; } = new();
    }

    public sealed class KrakenTrade
    {
        // ids
        [JsonPropertyName("ordertxid")] public string? OrderTxId { get; set; }
        [JsonPropertyName("postxid")] public string? PosTxId { get; set; }
        [JsonPropertyName("trade_id")] public long? TradeId { get; set; }  // sometimes 0 / absent

        // instrument
        [JsonPropertyName("pair")] public string? Pair { get; set; }  // e.g., "BCHUSD"
        [JsonPropertyName("aclass")] public string? AClass { get; set; }  // e.g., "forex"
        [JsonPropertyName("type")] public string? Type { get; set; }  // "buy" | "sell"
        [JsonPropertyName("ordertype")] public string? OrderType { get; set; } // "market", "limit", ...

        // times
        [JsonPropertyName("time")] public double? Time { get; set; } // epoch seconds (fractional)
                                                                     // helper to get DateTimeOffset if you want:
        [JsonIgnore]
        public DateTimeOffset? TimeUtc =>
            Time is double t ? DateTimeOffset.FromUnixTimeMilliseconds((long)(t * 1000.0)) : null;

        // amounts (arrive as strings; we convert to decimals)
        [JsonPropertyName("price")][JsonConverter(typeof(StringDecimalConverter))] public decimal? Price { get; set; }
        [JsonPropertyName("cost")][JsonConverter(typeof(StringDecimalConverter))] public decimal? Cost { get; set; }
        [JsonPropertyName("fee")][JsonConverter(typeof(StringDecimalConverter))] public decimal? Fee { get; set; }
        [JsonPropertyName("vol")][JsonConverter(typeof(StringDecimalConverter))] public decimal? Volume { get; set; }
        [JsonPropertyName("margin")][JsonConverter(typeof(StringDecimalConverter))] public decimal? Margin { get; set; }

        // leverage / misc flags
        [JsonPropertyName("leverage")] public string? Leverage { get; set; } // often "0"
        [JsonPropertyName("maker")] public bool? Maker { get; set; } // true if maker
        [JsonPropertyName("misc")] public string? Misc { get; set; }

        // future-proof: capture any extra fields Kraken may add
        [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
    }

    // string-or-number -> decimal converter
    public sealed class StringDecimalConverter : JsonConverter<decimal?>
    {
        public override decimal? Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null) return null;
            if (reader.TokenType == JsonTokenType.Number) return reader.GetDecimal();
            if (reader.TokenType == JsonTokenType.String &&
                decimal.TryParse(reader.GetString(), System.Globalization.NumberStyles.Any,
                                 System.Globalization.CultureInfo.InvariantCulture, out var d))
                return d;
            return null;
        }
        public override void Write(Utf8JsonWriter writer, decimal? value, JsonSerializerOptions options)
            => writer.WriteStringValue(value?.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    /*
   {
  "error": [],
  "result": {
    "count": 241,
    "trades": {
      "TH4B5F-BYCUB-DYBL2J": {
        "ordertxid": "OSHBUI-AFU6N-P37PNK",
        "postxid": "TKH2SE-M7IF5-CFI7LT",
        "pair": "BCHUSD",
        "aclass": "forex",
        "time": 1759960488.016865,
        "type": "buy",
        "ordertype": "market",
        "price": "583.165900",
        "cost": "4038.580000",
        "fee": "9.692592",
        "vol": "6.92526775",
        "margin": "0.000000",
        "leverage": "0",
        "misc": "",
        "trade_id": 0,
        "maker": false
      },
     */

    public sealed class KrakenTradesHistoryEntry
    {

    }

    public sealed class KrakenTickerEntry
        {
            [JsonPropertyName("a")] public string[]? Ask { get; set; }
            [JsonPropertyName("b")] public string[]? Bid { get; set; }
            [JsonPropertyName("c")] public string[]? LastTrade { get; set; }
            [JsonPropertyName("v")] public string[]? Volume { get; set; }
            [JsonPropertyName("p")] public string[]? Vwap { get; set; }
            [JsonPropertyName("t")] public int[]? Trades { get; set; }
            [JsonPropertyName("l")] public string[]? Low { get; set; }
            [JsonPropertyName("h")] public string[]? High { get; set; }
            [JsonPropertyName("o")] public string? Open { get; set; }
        }

    public class KrakenTickerParams
    {
        public string pair { get; set; } = "XBTUSD";
    }

    public sealed record TickerRow(
            string Pair,
            decimal Last,
            decimal Open,
            decimal ChangePct,
            decimal Bid,
            decimal Ask,
            decimal High24h,
            decimal Low24h,
            string Volume24h
        );

        public sealed record OhlcCandle(
            long Time,
            decimal Open,
            decimal High,
            decimal Low,
            decimal Close,
            decimal Vwap,
            decimal Volume,
            int Count
        );


    // Position snapshot while iterating trades
    public sealed class Position
    {
        public decimal Qty { get; set; }                // remaining quantity
        public decimal AvgCost { get; set; }            // average entry price (quote per unit)
        public decimal RealizedPnl { get; set; }        // realized P&L in quote currency
        public decimal FeesPaid { get; set; }           // total fees in quote currency
    }

}