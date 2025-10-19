using GooseGooseGo_Net.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Diagnostics;
using System.Globalization;
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

        public async Task<ApiResponse<cMexcAccounts?>> doMexcReturnPortfolioSimple(dbContext _dbCon)
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



        public async Task<ApiResponse<List<cMexcOrderLotSummaryGroup<string,cMexcOrderLotSummary>>>?> doMexcReturnPortfolio(dbContext _dbCon)
        {
            var ret = new ApiResponse<List<cMexcOrderLotSummaryGroup<string, cMexcOrderLotSummary>>>();
            ret.apiData = new List<cMexcOrderLotSummaryGroup<string, cMexcOrderLotSummary>>();
            try
            {
                // 1) balances
                var acct = await doApi_AccountsAsync(_dbCon);
                if (acct?.balances is null)
                {
                    ret.apiSuccess = true;
                    ret.apiData = new List<cMexcOrderLotSummaryGroup<string, cMexcOrderLotSummary>>();
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

                    cMexcOrderLotSummaryGroup<string,cMexcOrderLotSummary> trades = await doApi_TradesByOrderAsync(_dbCon, symbol);
                    ret.apiData.Add(trades);
                }
                ret.apiSuccess = true;
                return ret;
            }
            catch (Exception ex)
            {
                ret.apiSuccess = false;
                ret.apiMessage = ex.Message;
                return ret;
            }
        }

        public async Task<ApiResponse<List<cMexcPortfolio>>?> doMexcReturnPortfolio2(dbContext _dbCon)
        {
            bool method_new = true; // switch between trade-fetching methods

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
            string retJson = await doApi_Base(_dbCon, p);

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
            string retJson = await doApi_Base(_dbCon, p);

            var ret = JsonSerializer.Deserialize<List<MexcTickerEntry>>(retJson);

            return ret;
        }

        // balances (you already have)
        public async Task<cMexcAccounts?> doApi_AccountsAsync(dbContext _dbCon, CancellationToken ct = default)
        {
            cMexcAccounts? ret = null;
            var p = new cApiParms { 
                apMethod = "GET",
                apPath = "/api/v3/account",
                apDoSign = true,              // <<— required for private endpoints
                apQuery = new()
            };
            string retJson = await doApi_Base(_dbCon, p, ct);           // your HMAC-signed/base method
            ret =  JsonSerializer.Deserialize<cMexcAccounts>(retJson);

            return ret;
        }

        // all prices (public)
        public async Task<List<MexcTickerPrice>?> doApi_TickerPrice_AllAsync(dbContext _dbCon)
        {
            List<MexcTickerPrice>? ret = null;
            var p = new cApiParms { apMethod = "GET", apPath = "/api/v3/ticker/price", apDoSign = false };
            string retJson = await doApi_Base(_dbCon, p); // public endpoint
            ret = JsonSerializer.Deserialize<List<MexcTickerPrice>>(retJson)
                   ?? new List<MexcTickerPrice>();
            return ret;
        }

        public async Task<cMexcOrderLotSummaryGroup<string, cMexcOrderLotSummary>> doApi_TradesByOrderAsync(dbContext _dbCon, string symbol)
        {
            cMexcOrderLotSummaryGroup<string, cMexcOrderLotSummary>? ret = null;

            // 1) full trade list for symbol (paginate if needed; limit 100 per page on MEXC)
            var trades = await doApi_MyTradesAsync(_dbCon, symbol, limit: 100);
            // TODO: loop with fromId to pull more pages if you need history
            trades = trades.OrderBy(t => t.time).ToList();

            // 2) current price once
            var last = await GetLastPriceAsync(_dbCon, symbol);

            // 3) group all trades by orderId
            var byOrder = trades.GroupBy(t => t.orderId).ToList();

            var lots = new List<cMexcOrderLotSummary>();

            foreach (var grp in byOrder)
            {
                // We only create a "lot" for BUY orders
                var buys = grp.Where(t => t.isBuyer).OrderBy(t => t.time).ToList();
                if (!buys.Any())
                    continue;

                // (a) compute the buy lot totals for this orderId
                decimal buyQty = 0m, buyCostQ = 0m, buyFeesQ = 0m;
                long filledTime = buys.Last().time; // use last fill as "lot filled" time

                foreach (var t in buys)
                {
                    var price = ParseDec(t.price);
                    var qty = ParseDec(t.qty);
                    var feeQ = FeeToQuote(t, price);
                    buyQty += qty;
                    buyCostQ += price * qty + feeQ;
                    buyFeesQ += feeQ;
                }
                if (buyQty <= 0) continue;

                var buyAvg = buyCostQ / buyQty;

                // (b) consume later sells to compute remaining qty and realized PnL for THIS lot
                decimal remaining = buyQty;
                decimal realized = 0m;

                foreach (var s in trades) // all trades after the lot’s fills
                {
                    if (s.time <= filledTime) continue;
                    if (remaining <= 0) break;
                    if (s.isBuyer) continue;

                    var sellPrice = ParseDec(s.price);
                    var sellQty = ParseDec(s.qty);
                    var feeQ = FeeToQuote(s, sellPrice);

                    var use = Math.Min(remaining, sellQty);
                    if (use <= 0) continue;

                    // attribute a proportional part of the sell fee to the consumed qty
                    realized += (sellPrice - buyAvg) * use - feeQ * (use / sellQty);
                    remaining -= use;
                }

                // (c) live mark-to-market
                var mv = remaining * last;
                var unrl = (last - buyAvg) * remaining;

                lots.Add(new cMexcOrderLotSummary
                {
                    mpolsSymbol = symbol,
                    mpolsOrderId = grp.Key,
                    mpolsFilledAt = DateTimeOffset.FromUnixTimeMilliseconds(filledTime).UtcDateTime,

                    mpolsBuyQty = buyQty,
                    mpolsBuyCostQuote = buyCostQ,
                    mpolsBuyAvgCost = buyAvg,
                    mpolsBuyFeesQuote = buyFeesQ,
                    mpolsRemainingQty = remaining,
                    mpolsRealizedPnlFromThisLot = realized,
                    mpolsCurrentPrice = last,
                    mpolsMarketValue = mv,
                    mpolsUnrealizedPnl = unrl
                });
            }

            // Most recent first
            ret = new cMexcOrderLotSummaryGroup<string, cMexcOrderLotSummary>
            {
                Key = symbol,
                Items = lots.OrderByDescending(l => l.mpolsFilledAt).ToList()
            };
            //return lots.OrderByDescending(l => l.FilledAt).ToList();
            return ret;
        }

        // --- helpers (same as before) ---
        private static decimal ParseDec(string? s) =>
            decimal.TryParse(s, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0m;

        private static string BaseFromSymbol(string sym)
        {
            if (sym.EndsWith("USDT", StringComparison.OrdinalIgnoreCase)) return sym[..^4];
            if (sym.EndsWith("USDC", StringComparison.OrdinalIgnoreCase)) return sym[..^4];
            if (sym.EndsWith("USD", StringComparison.OrdinalIgnoreCase)) return sym[..^3];
            return sym;
        }
        private static string QuoteFromSymbol(string sym)
        {
            if (sym.EndsWith("USDT", StringComparison.OrdinalIgnoreCase)) return "USDT";
            if (sym.EndsWith("USDC", StringComparison.OrdinalIgnoreCase)) return "USDC";
            if (sym.EndsWith("USD", StringComparison.OrdinalIgnoreCase)) return "USD";
            return "USDT";
        }
        private static decimal FeeToQuote(MexcMyTrade t, decimal tradePrice)
        {
            var fee = ParseDec(t.commission);
            if (fee == 0) return 0;
            var quote = QuoteFromSymbol(t.symbol);
            var @base = BaseFromSymbol(t.symbol);

            if (t.commissionAsset?.Equals(quote, StringComparison.OrdinalIgnoreCase) == true)
                return fee;
            if (t.commissionAsset?.Equals(@base, StringComparison.OrdinalIgnoreCase) == true)
                return fee * tradePrice;

            // Fees in MX or something else: omit or convert via its price if you wish.
            return 0;
        }
        private async Task<decimal> GetLastPriceAsync(dbContext _dbCon, string symbol)
        {
            var all = await doApi_TickerPrice_AllAsync(_dbCon);
            var m = all.FirstOrDefault(x => x.symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));
            return ParseDec(m?.price);
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

                var retJson = await doApi_Base(_dbCon, p); // this adds timestamp/signature + header
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

        public async Task<cMexcExchangeInfo> doApi_ExchangeInfoAsync(dbContext _dbCon, string symbol, CancellationToken ct)
        {
            var p = new cApiParms { apMethod = "GET", apPath = "/api/v3/exchangeInfo", apQuery = new() { ["symbol"] = symbol } };
            var json = await doApi_Base(_dbCon, p, ct);
            return JsonSerializer.Deserialize<cMexcExchangeInfo>(json)!;
        }

        public async Task<cMexcBookTicker?> doApi_TickerBookAsync(dbContext _dbCon, string symbol, CancellationToken ct)
        {
            var p = new cApiParms { apMethod = "GET", apPath = "/api/v3/ticker/bookTicker", apQuery = new() { ["symbol"] = symbol } };
            var json = await doApi_Base(_dbCon, p, ct);
            return JsonSerializer.Deserialize<cMexcBookTicker>(json);
        }

        public async Task<cMexcSellPreview> doMexcPreviewSell100Async(dbContext _dbCon, cMexcSellParms p, CancellationToken ct = default)
        {
            string symbol = p.mxsellSymbol.ToUpperInvariant();                // e.g., "JUMPUSDT"
            var baseAsset = symbol.EndsWith("USDT") ? symbol[..^4] :
                             symbol.EndsWith("USDC") ? symbol[..^4] :
                             symbol.EndsWith("USD") ? symbol[..^3] : throw new("Unsupported quote");

            // a) balance (free only for spot)
            var acct = await doApi_AccountsAsync(_dbCon, ct); // signed
            var bal = acct?.balances?.FirstOrDefault(b => string.Equals(b.asset, baseAsset, StringComparison.OrdinalIgnoreCase));
            var free = ParseDec(bal?.free);

            if (free <= 0) return new(symbol, 0, 0, 0, 0, "No free balance.");

            // b) symbol filters
            cMexcExchangeInfo ex = await doApi_ExchangeInfoAsync(_dbCon, symbol, ct); // GET /api/v3/exchangeInfo?symbol=...
            //var lot = ex.filters.First(f => f.filterType == "LOT_SIZE");
            //var priceF = ex.filters.First(f => f.filterType == "PRICE_FILTER");
            //var notionalF = ex.filters.FirstOrDefault(f => f.filterType == "MIN_NOTIONAL"); // sometimes present
            var sym = ex.symbols.FirstOrDefault()
          ?? throw new InvalidOperationException($"Symbol '{symbol}' not found in exchangeInfo.");

            var lot = sym.filters.FirstOrDefault(f => f.filterType == "LOT_SIZE")
                      ?? throw new InvalidOperationException("LOT_SIZE filter missing.");

            var priceF = sym.filters.FirstOrDefault(f => f.filterType == "PRICE_FILTER")
                         ?? throw new InvalidOperationException("PRICE_FILTER missing.");

            var notionalF = sym.filters.FirstOrDefault(f => f.filterType == "MIN_NOTIONAL"); // may be null on some pairs

            var stepSize = ParseDec(lot.stepSize);
            var minQty = ParseDec(lot.minQty);
            var tickSize = ParseDec(priceF.tickSize);
            var minNotion = notionalF != null ? ParseDec(notionalF.minNotional) : 0;

            // c) best bid for estimate
            var book = await doApi_TickerBookAsync(_dbCon, symbol, ct); // /api/v3/ticker/bookTicker?symbol=...
            var bid = ParseDec(book?.bidPrice);
            if (bid <= 0) return new(symbol, free, 0, 0, 0, "No bid price.");

            // d) compute sellable qty (floor to stepSize)
            decimal qty = FloorToStep(free, stepSize);
            if (qty < minQty) return new(symbol, free, 0, bid, 0, $"Below minQty ({minQty}).");

            // e) min notional check (if present)
            var estProceeds = qty * bid;
            if (minNotion > 0 && estProceeds < minNotion)
            {
                // try bump down to meet minNotional via flooring
                var needed = minNotion / bid;
                if (qty < needed) return new(symbol, free, 0, bid, 0, $"Below minNotional ({minNotion}).");
                qty = FloorToStep(needed, stepSize);
                estProceeds = qty * bid;
            }

            return new(symbol, free, qty, bid, estProceeds);
        }

        public async Task<object> doMexcExecuteSell100Async(dbContext _dbCon, cMexcSellParms pIn, CancellationToken ct)
        {
            // POST /api/v3/order (signed)
            var p = new cApiParms
            {
                apMethod = "POST",
                apPath = "/api/v3/order",
                apDoSign = true,
                apQuery = new Dictionary<string, string>
                {
                    ["symbol"] = pIn.mxsellSymbol,
                    ["side"] = "SELL",
                    ["type"] = "MARKET",
                    ["quantity"] = pIn.mxsellQty.ToString(CultureInfo.InvariantCulture),
                    ["newOrderRespType"] = "RESULT"
                }
            };
            var json = await doApi_Base(_dbCon, p, ct);
            // Return raw or map to your typed response
            return JsonSerializer.Deserialize<JsonElement>(json);
        }

        // helpers
        // private static decimal ParseDec(string? s) => decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m;
        private static decimal FloorToStep(decimal value, decimal step)
        {
            if (step <= 0) return value;
            var steps = Math.Floor(value / step);
            return steps * step;
        }

        public async Task<string> doApi_Base(dbContext _dbCon, cApiParms p, CancellationToken ct = default)
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

        private static string BaseFromSymbol2(string symbol)
        {
            if (symbol.EndsWith("USDT", StringComparison.OrdinalIgnoreCase)) return symbol[..^4];
            if (symbol.EndsWith("USDC", StringComparison.OrdinalIgnoreCase)) return symbol[..^4];
            if (symbol.EndsWith("USD", StringComparison.OrdinalIgnoreCase)) return symbol[..^3];
            // crude fallback
            return symbol;
        }

        private static decimal ParseDec2(string? s)
            => decimal.TryParse(s, System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture, out var d)
               ? d : 0m;

    }

    // --- Model Classes ---

    public class cMexcExchangeInfo { public List<cMexcSymbol> symbols { get; set; } = new(); public cMexcSymbol First() => symbols.First(); }
    public class cMexcSymbol { public string symbol { get; set; } = ""; public List<cMexcFilter> filters { get; set; } = new(); }
    public class cMexcFilter { public string filterType { get; set; } = ""; public string minQty { get; set; } = "0"; public string stepSize { get; set; } = "0"; public string tickSize { get; set; } = "0"; public string minNotional { get; set; } = "0"; }
    public class cMexcBookTicker { public string symbol { get; set; } = ""; public string bidPrice { get; set; } = "0"; public string bidQty { get; set; } = "0"; }

    public record cMexcSellRequest(string Symbol, bool DryRun = true);

    public record cMexcSellPreview(
        string Symbol, decimal FreeQty, decimal QtyToSell, decimal BidPrice,
        decimal EstProceeds, string ReasonIfBlocked = "");


    public class cMexcSellParms
    {
        public string mxsellSymbol { get; set; } = "";
        public decimal mxsellQty { get; set; } = 100.0m;
        public bool mxsellDryRun { get; set; } = true;
    }

    public class cMexcOrderLotSummaryGroup<TKey, TItem>
    {
        public TKey Key { get; init; } = default!;
        public List<TItem> Items { get; init; } = new();
    }


    public class cMexcOrderLotSummary
    {
        public string mpolsSymbol { get; set; } = "";
        public string mpolsOrderId { get; set; } = "";
        public DateTime mpolsFilledAt { get; set; }

        public decimal mpolsBuyQty { get; set; }            // total qty filled on that orderId (buys only)
        public decimal mpolsBuyCostQuote { get; set; }      // Σ(price*qty + buy-fees-in-quote)
        public decimal mpolsBuyAvgCost { get; set; }        // BuyCostQuote / BuyQty
        public decimal mpolsBuyFeesQuote { get; set; }      // fees charged on the buy fills

        public decimal mpolsRemainingQty { get; set; }      // after subsequent sells
        public decimal mpolsRealizedPnlFromThisLot { get; set; } // realized due to sells after this order
        public decimal mpolsCurrentPrice { get; set; }
        public decimal mpolsMarketValue { get; set; }
        public decimal mpolsUnrealizedPnl { get; set; }
    }


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