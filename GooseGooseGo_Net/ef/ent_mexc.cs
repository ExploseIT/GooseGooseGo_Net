using GooseGooseGo_Net.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static GooseGooseGo_Net.ef.ent_asset;
using static System.Net.WebRequestMethods;

namespace GooseGooseGo_Net.ef
{
    /// <summary>
    /// Singleton-safe Mexc logic. No per-instance mutable state, DI for singleton-safe services.
    /// Pass dbContext as method argument for DB operations.
    /// Uses IHttpClientFactory for HTTP calls.
    /// </summary>
    public class ent_mexc : cIsDbNull
    {
        private readonly IConfiguration _conf;
        private readonly ILogger<mApp> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _encryptionKey;
        private readonly cApiDetails? _apiDetails;

        Exception? exc = null;

        public ent_mexc(
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
            var set_mexc_api = _e_settings.doSettingsReadByName("Mexc_API");
            if (set_mexc_api == null)
            {
                var cs = _conf.GetSection("MEXC_API");
                string _apiUrl = cs.GetValue<string>("MEXC_API_URL") ?? "";
                string _apiKey = cs.GetValue<string>("MEXC_KEY") ?? "";
                string _apiSecret = cs.GetValue<string>("MEXC_SECRET") ?? "";

                var cMexcApiDetails = new cMexcApiDetails
                {
                    apidet_api_url = _apiUrl,
                    apidet_key = _apiKey,
                    apidet_secret = _apiSecret
                };
                string json = JsonSerializer.Serialize(cMexcApiDetails);
                ret = mEncryption.EncryptString(json, _encryptionKey);
                _e_settings.doSettingsInsertByName("MEXC_API", ret, "Encrypted Mexc API details");
            }
            return ret;
        }

        public cApiDetails? doApiDetailsDecrypt(dbContext dbCon)
        {
            cApiDetails? ret = null;
            try
            {
                string api_index = "MEXC_API";
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
                _logger.LogError(ex, "Error decrypting Mexc API details");
            }
            return ret;
        }

        public async Task<ApiResponse<cMexcAccounts?>> doMexcReturnPortfolio2(dbContext _dbCon)
        {
            ApiResponse<cMexcAccounts?> ret = new ApiResponse<cMexcAccounts?>();
            try
            {
                ret.apiData = await doApi_AccountsAsync(_dbCon);
            }
            catch (Exception ex)
            {
                ret.apiSuccess = false;
                ret.apiMessage = ex.Message;
            }
            return ret;
        }

        public async Task<ApiResponse<List<cMexcPortfolio>>?> doMexcReturnPortfolio(dbContext _dbCon)
        {
            var ret = new ApiResponse<List<cMexcPortfolio>>();
            try
            {
                // 1) balances
                var acct = await doApi_AccountsAsync(_dbCon);
                if (acct?.balances is null)
                {
                    ret.apiSuccess = true;
                    ret.apiData = new List<cMexcPortfolio>();
                    return ret;
                }

                // 2) collect assets with non-zero total
                var assets = acct.balances
                    .Select(b =>
                    {
                        decimal free = ParseDec(b.free);
                        decimal locked = ParseDec(b.locked) + ParseDec(b.available); // MEXC sometimes provides both
                        return new { asset = b.asset?.Trim() ?? "", qty = free + locked };
                    })
                    .Where(x => x.asset.Length > 0 && x.qty > 0m)
                    .ToList();

                // 3) build preferred symbols (USDT first, then USDC, then USD)
                static IEnumerable<string> CandidateSymbols(string asset) =>
                    new[] { $"{asset}USDT", $"{asset}USDC", $"{asset}USD" };

                var wantedSymbols = assets
                    .SelectMany(a => CandidateSymbols(a.asset))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                // 4) get all prices (batch)
                var allPrices = await doApi_TickerPrice_AllAsync(_dbCon); // calls /api/v3/ticker/price (no auth)
                var priceMap = allPrices?
                    .GroupBy(tp => tp.symbol, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => ParseDec(g.First().price), StringComparer.OrdinalIgnoreCase)
                    ?? new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

                // 5) per-asset pick first existing symbol and compute PnL using trade history
                var portfolios = new List<cMexcPortfolio>();
                foreach (var a in assets)
                {
                    string? symbol = CandidateSymbols(a.asset).FirstOrDefault(s => priceMap.ContainsKey(s));
                    if (symbol is null)
                    {
                        // no price found; skip or include with zeros
                        portfolios.Add(new cMexcPortfolio
                        {
                            mpAsset = a.asset,
                            mpQtyHeld = a.qty,
                            mpAvgCost = 0,
                            mpLastPrice = 0,
                            mpMarketValue = 0,
                            mpUnrealizedPnl = 0,
                            mpRealizedPnl = 0,
                            mpFeesPaid = 0,
                            mpRetrievedAt = DateTime.UtcNow
                        });
                        continue;
                    }

                    decimal lastPrice = priceMap[symbol];

                    // 6) trades for the symbol (paginate if needed)
                    var trades = await doApi_MyTradesAsync(_dbCon, symbol, limit: 1000);

                    // 7) compute moving-average avgCost, realized pnl, fees in QUOTE
                    ComputePnL_MovingAverage(
                        trades,
                        out decimal avgCost,
                        out decimal realizedPnl,
                        out decimal feesQuote);

                    // 8) unrealized on current inventory
                    decimal qty = a.qty;
                    decimal unrealized = (qty > 0 && avgCost > 0) ? (lastPrice - avgCost) * qty : 0m;
                    decimal mv = qty * lastPrice;

                    portfolios.Add(new cMexcPortfolio
                    {
                        mpAsset = a.asset,
                        mpQtyHeld = qty,
                        mpAvgCost = avgCost,
                        mpLastPrice = lastPrice,
                        mpMarketValue = mv,
                        mpUnrealizedPnl = unrealized,
                        mpRealizedPnl = realizedPnl,
                        mpFeesPaid = feesQuote,
                        mpRetrievedAt = DateTime.UtcNow
                    });
                }

                ret.apiSuccess = true;
                ret.apiData = portfolios;
                return ret;
            }
            catch (Exception ex)
            {
                ret.apiSuccess = false;
                ret.apiMessage = ex.Message;
                return ret;
            }
        }


        public async Task<cMexcAccounts?> doApi_Accounts2Async(dbContext _dbCon)
        {
            cApiParms p = new cApiParms
            {
                apMethod = "GET",
                apPath = "/api/v3/account",
            };
            string retJson = await doApi_Base(p, _dbCon);

            var ret = JsonSerializer.Deserialize<cMexcAccounts>(retJson);

            return ret;
        }

        public async Task<List<MexcTickerEntry>?> doApi_TickerListAsync(dbContext _dbCon)
        {
            cApiParms p = new cApiParms
            {
                apMethod = "GET",
                apPath = "/api/v3/ticker/24hr"
            };
            string retJson = await doApi_Base(p, _dbCon);

            var ret = JsonSerializer.Deserialize<List<MexcTickerEntry>>(retJson);

            return ret;
        }

        // balances (you already have)
        public async Task<cMexcAccounts?> doApi_AccountsAsync(dbContext _dbCon)
        {
            cMexcAccounts? ret = null;
            var p = new cApiParms { 
                apMethod = "GET",
                apPath = "/api/v3/account",
                apDoSign = true,              // <<— required for private endpoints
                apQuery = new()
            };
            string retJson = await doApi_Base(p, _dbCon);           // your HMAC-signed/base method
            ret =  JsonSerializer.Deserialize<cMexcAccounts>(retJson);

            return ret;
        }

        // all prices (public)
        public async Task<List<MexcTickerPrice>?> doApi_TickerPrice_AllAsync(dbContext _dbCon)
        {
            List<MexcTickerPrice>? ret = null;
            var p = new cApiParms { apMethod = "GET", apPath = "/api/v3/ticker/price", apDoSign = false };
            string retJson = await doApi_Base(p, _dbCon); // public endpoint
            ret = JsonSerializer.Deserialize<List<MexcTickerPrice>>(retJson)
                   ?? new List<MexcTickerPrice>();
            return ret;
        }

        // user trades for one symbol (private)
        public async Task<List<MexcMyTrade>?> doApi_MyTradesAsync(
            dbContext _dbCon, string symbol, int limit = 100, long? fromId = null)
        {
            List<MexcMyTrade>? ret = null;

            try
            {
                // MEXC max = 100
                if (limit > 100) limit = 100;

                var q = new Dictionary<string, string>
                {
                    ["symbol"] = symbol.ToUpperInvariant(),
                    ["limit"] = limit.ToString()
                };
                if (fromId.HasValue) q["fromId"] = fromId.Value.ToString();

                var p = new cApiParms
                {
                    apMethod = "GET",
                    apPath = "/api/v3/myTrades",
                    apQuery = q,
                    apDoSign = true   // 🔴 REQUIRED for private endpoints
                };

                var retJson = await doApi_Base(p, _dbCon); // this adds timestamp/signature + header
                ret = JsonSerializer.Deserialize<List<MexcMyTrade>>(retJson)
                              ?? new List<MexcMyTrade>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching MEXC trades for {Symbol}", symbol);
                exc = ex;
            }
            return ret;
        }



        static long _srvOffsetMs = 0;
        static long _lastSyncUnixMs = 0;
        static readonly TimeSpan SyncTtl = TimeSpan.FromMinutes(5);

        async Task EnsureTimeSyncAsync(HttpClient client)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (now - _lastSyncUnixMs < (long)SyncTtl.TotalMilliseconds) return;

            using var res = await client.GetAsync("https://api.mexc.com/api/v3/time");
            res.EnsureSuccessStatusCode();
            var json = await res.Content.ReadAsStringAsync();
            var doc = System.Text.Json.JsonDocument.Parse(json);
            var serverTime = doc.RootElement.GetProperty("serverTime").GetInt64();

            _srvOffsetMs = serverTime - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _lastSyncUnixMs = now;
        }

        // Helper: URL-encode and join k=v with '&'
        private static string BuildQueryString(IEnumerable<KeyValuePair<string, string>> kvs)
        {
            return string.Join("&", kvs.Select(kv =>
                $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value ?? string.Empty)}"));
        }

        public async Task<string> doApi_Base(cApiParms p, dbContext _dbCon, CancellationToken ct = default)
        {
            string ret = "";
            HttpResponseMessage? resp = null;
            try
            {
                using var client = _httpClientFactory.CreateClient("mexc");

                // Start from caller-supplied query params (dictionary may be empty)
                var qList = new List<KeyValuePair<string, string>>();
                if (p.apQuery is not null)
                {
                    foreach (var kv in p.apQuery)
                        qList.Add(new KeyValuePair<string, string>(kv.Key, kv.Value ?? string.Empty));
                }

                string url;
                var method = new HttpMethod(p.apMethod);

                if (p.apDoSign)
                {
                    // Append recvWindow then timestamp (order matters because we sign this exact string)
                    var recvWindow = "5000";
                    var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

                    qList.Add(new("recvWindow", recvWindow));
                    qList.Add(new("timestamp", ts));

                    // Build the canonical query string that we will ALSO send
                    var qs = BuildQueryString(qList);

                    // Sign that exact string
                    var signature = Sign(_apiDetails!.apidet_secret, qs);

                    // Compose final URL (signature as last param)
                    url = $"{_apiDetails.apidet_api_url}{p.apPath}?{qs}&signature={signature}";
                }
                else
                {
                    // Public endpoint: just use whatever params were passed
                    var qs = qList.Count > 0 ? $"?{BuildQueryString(qList)}" : string.Empty;
                    url = $"{_apiDetails!.apidet_api_url}{p.apPath}{qs}";
                }

                var req = new HttpRequestMessage(method, url);

                if (p.apDoSign)
                    req.Headers.TryAddWithoutValidation("X-MEXC-APIKEY", _apiDetails.apidet_key);

                if (!string.IsNullOrEmpty(p.apBody))
                    req.Content = new StringContent(p.apBody, System.Text.Encoding.UTF8, "application/json");

                resp = await client.SendAsync(req, ct);
                ret = await resp.Content.ReadAsStringAsync(ct);
                resp.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _logger.LogInformation("MEXC req: {Method} {Url} signed={Signed} status={Status}",
    p.apMethod, _apiDetails!.apidet_api_url, p.apDoSign, resp?.StatusCode);

                exc = ex;
            }

            return ret;
        }

        // HMAC-SHA256 hex signer
        private static string Sign(string secret, string payload)
        {
            var key = System.Text.Encoding.UTF8.GetBytes(secret ?? "");
            var data = System.Text.Encoding.UTF8.GetBytes(payload ?? "");
            using var h = new System.Security.Cryptography.HMACSHA256(key);
            var hash = h.ComputeHash(data);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }



        private static void ComputePnL_MovingAverage(
    List<MexcMyTrade> trades,
    out decimal avgCost, out decimal realizedPnl, out decimal feesInQuote)
        {
            avgCost = 0m;
            realizedPnl = 0m;
            feesInQuote = 0m;

            decimal posQty = 0m;
            decimal totalCost = 0m; // in quote for current inventory

            foreach (var t in trades.OrderBy(t => t.time))
            {
                decimal price = ParseDec(t.price);
                decimal qty = ParseDec(t.qty);
                decimal fee = ParseDec(t.commission);

                // Determine quote currency (last 3-4 chars): e.g., BTCUSDT -> USDT
                string quote = InferQuote(t.symbol);

                // normalize fee to quote
                decimal feeQuote = 0m;
                if (!string.IsNullOrEmpty(t.commissionAsset))
                {
                    if (t.commissionAsset.Equals(quote, StringComparison.OrdinalIgnoreCase))
                        feeQuote = fee;
                    else
                    {
                        // if fee charged in base, convert to quote using trade price
                        // otherwise (rare), ignore or extend with a map if you need exact conversion
                        if (t.commissionAsset.Equals(BaseFromSymbol(t.symbol), StringComparison.OrdinalIgnoreCase))
                            feeQuote = fee * price;
                    }
                }

                feesInQuote += feeQuote;

                if (t.isBuyer)
                {
                    // buy increases inventory and raises totalCost by price*qty + fee
                    totalCost += price * qty + feeQuote;
                    posQty += qty;

                    avgCost = posQty > 0 ? totalCost / posQty : 0m;
                }
                else
                {
                    // sell realizes pnl on qty
                    if (posQty <= 0)
                    {
                        // short (shouldn't happen on spot) – treat as realized straight
                        realizedPnl += (price * qty) - feeQuote; // no cost to offset
                        continue;
                    }

                    decimal sellQty = qty;
                    if (sellQty > posQty) sellQty = posQty; // cap to position

                    // realized pnl uses current avgCost
                    realizedPnl += (price - avgCost) * sellQty - feeQuote;

                    // reduce inventory cost basis
                    decimal costPortion = avgCost * sellQty;
                    totalCost -= costPortion;
                    posQty -= sellQty;

                    avgCost = posQty > 0 ? totalCost / posQty : 0m;

                    // if a sell bigger than posQty occurred, the extra part was ignored (no short on spot)
                }
            }

            // note: unrealized is computed by caller with (last - avg) * posQty
        }

        private static string InferQuote(string symbol)
        {
            // order matters
            if (symbol.EndsWith("USDT", StringComparison.OrdinalIgnoreCase)) return "USDT";
            if (symbol.EndsWith("USDC", StringComparison.OrdinalIgnoreCase)) return "USDC";
            if (symbol.EndsWith("USD", StringComparison.OrdinalIgnoreCase)) return "USD";
            return symbol.Length >= 3 ? symbol[^3..] : "USDT";
        }

        private static string BaseFromSymbol(string symbol)
        {
            if (symbol.EndsWith("USDT", StringComparison.OrdinalIgnoreCase)) return symbol[..^4];
            if (symbol.EndsWith("USDC", StringComparison.OrdinalIgnoreCase)) return symbol[..^4];
            if (symbol.EndsWith("USD", StringComparison.OrdinalIgnoreCase)) return symbol[..^3];
            // crude fallback
            return symbol;
        }

        private static decimal ParseDec(string? s)
            => decimal.TryParse(s, System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture, out var d)
               ? d : 0m;

    }

    // --- Model Classes ---

    public class cMexcPortfolio
    {
        [Key] public int mpId { get; set; }
        public string mpAsset { get; set; } = "";
        [Precision(18, 8)] public decimal mpQtyHeld { get; set; }
        [Precision(18, 8)] public decimal mpAvgCost { get; set; }          // in quote (USDT) per unit
        [Precision(18, 8)] public decimal mpLastPrice { get; set; }        // in quote (USDT)
        [Precision(18, 8)] public decimal mpMarketValue { get; set; }      // qty * last
        [Precision(18, 8)] public decimal mpUnrealizedPnl { get; set; }    // (last - avg) * qty
        [Precision(18, 8)] public decimal mpRealizedPnl { get; set; }      // realized in quote
        [Precision(18, 8)] public decimal mpFeesPaid { get; set; }         // fees in quote
        public DateTime mpRetrievedAt { get; set; }
        public DateTime mpRetrievedAtSanitised()
        {
            var dt = mpRetrievedAt.ToLocalTime();
            return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second);
        }
    }

    /// /api/v3/ticker/price (array)
    public class MexcTickerPrice
    {
        public string symbol { get; set; } = "";
        public string price { get; set; } = "0";
    }

    /// /api/v3/myTrades (array)
    public class MexcMyTrade
    {
        public string symbol { get; set; } = "";
        public string id { get; set; } = "";
        public string orderId { get; set; } = "";
        public string price { get; set; } = "0";
        public string qty { get; set; } = "0";
        public string quoteQty { get; set; } = "0";
        public string commission { get; set; } = "0";
        public string commissionAsset { get; set; } = "";
        public bool isBuyer { get; set; }   // true = buy, false = sell
        public bool isMaker { get; set; }
        public bool isBestMatch { get; set; } = true;
        public bool isSelfTrade { get; set; } = false;
        public string clientOrderId { get; set; } = "";
        public long time { get; set; }      // ms epoch
    }


    public class cMexcAccounts
    {
        public string? makerCommission { get; set; } = null;
        public string? takerCommission { get; set; } = null;
        public string? buyerCommission { get; set; } = null;
        public string? sellerCommission { get; set; } = null;
        public bool? canTrade { get; set; } = null;
        public bool? canWithdraw { get; set; } = null;
        public bool? canDeposit { get; set; } = null;
        public string? updateTime { get; set; } = null;
        public string? accountType { get; set; } = null;
        public string[]? permissions { get; set; } = null;
        public List<cMexcAccountEntry>? balances { get; set; } = null;
    }

    public class cMexcAccountEntry
    {
        public string? asset { get; set; } = null;
        public string? free { get; set; } = null;
        public string? locked { get; set; } = null;
        public string? available { get; set; } = null;
    }

    public class cMexcAssetInfo
    {
        [Key]
        public int kaiId { get; set; }
        public DateTime kaiDT { get; set; }
    }

    public class cMexc
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

    public class cMexcPercentageSwingParms
    {
        [Precision(18, 5)]
        public decimal kapsMinSwing { get; set; } = 0.0M;
        public int kapsPeriodValue { get; set; } = 0;
        public string kapsPeriodUnit { get; set; } = "";
        public int kapsRowCount { get; set; } = 5;
        public int kapsPeriodOffset { get; set; } = 0;
    }

    public class cMexcInfo
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

    public class cMexcPercentageSwing
    {
        [Key]
        public string kapsPair { get; set; } = "";
        [Precision(18, 5)]
        public decimal kapsStartTrade { get; set; } = 0.0M;
        [Precision(18, 5)]
        public decimal kapsEndTrade { get; set; } = 0.0M;
        [Precision(18, 5)]
        public decimal kapsTradeDiffPercent { get; set; } = 0.0M;
        [Precision(18, 5)]
        public decimal kapsTradeDiff { get; set; } = 0.0M;
        [Precision(18, 5)]
        public decimal kapsTradeDiffAbs { get; set; } = 0.0M;
        [Precision(18, 5)]
        public decimal kapsStartVolume { get; set; } = 0.0M;
        [Precision(18, 5)]
        public decimal kapsEndVolume { get; set; } = 0.0M;
        public DateTime kapsStartRetrievedAt { get; set; }
        public DateTime kapsEndRetrievedAt { get; set; }
    }

    public sealed record MexcEnvelope<T>(
        [property: JsonPropertyName("error")] List<string> Error,
        [property: JsonPropertyName("result")] T? Result
    );

    public class MexcTickerEntry
    {
        /*"symbol": "METALUSDT",
        "priceChange": "-0.013",
        "priceChangePercent": "-0.0319",
        "prevClosePrice": "0.40666",
        "lastPrice": "0.39366",
        "bidPrice": "0.39337",
        "bidQty": "3.24",
        "askPrice": "0.3941",
        "askQty": "121.34",
        "openPrice": "0.40666",
        "highPrice": "0.41618",
        "lowPrice": "0.39019",
        "volume": "89050.54",
        "quoteVolume": "35717.78507",
        "openTime": 1759627383256,
        "closeTime": 1759627447124,
        "count": null*/
        public string symbol { get; set; } = "";
        public string priceChange { get; set; } = "";
        public string priceChangePercent { get; set; } = "";
        public string prevClosePrice { get; set; } = "";
        public string lastPrice { get; set; } = "";
        public string bidPrice { get; set; } = "";
        public string bidQty { get; set; } = "";
        public string askPrice { get; set; } = "";
        public string askQty { get; set; } = "";
        public string openPrice { get; set; } = "";
        public string highPrice { get; set; } = "";
        public string volume { get; set; } = "";
        public string quoteVolume { get; set; } = "";
        public Int64 openTime { get; set; } = 0;
        public Int64 closeTime { get; set; } = 0;
        public string count { get; set; } = "";
    }

    public class cMexcApiDetails
    {
        public string apidet_api_url { get; set; } = "";
        public string apidet_key { get; set; } = "";
        public string apidet_secret { get; set; } = "";
    }
}