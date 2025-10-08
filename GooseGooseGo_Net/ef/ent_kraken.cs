
using GooseGooseGo_Net.Models;
using GooseGooseGo_Net.ef;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http;
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

        public async Task<KrakenEnvelope<Dictionary<string, KrakenTickerEntry>>?> doApi_TickerListAsync(dbContext _dbCon)
        {
            cApiParms p = new cApiParms
            {
                apMethod = "GET",
                apPath = "/0/public/Ticker",
                apDoSign = false
            };
            string retJson = await doApi_Base(p, _dbCon);

            var ret = JsonSerializer.Deserialize<KrakenEnvelope<Dictionary<string, KrakenTickerEntry>>?>(retJson);

            return ret;
        }

        public async Task<KrakenEnvelope<Dictionary<string, string>>?> doApi_AssetBalanceAsync(dbContext _dbCon)
        {
            cApiParms p = new cApiParms
            {
                apMethod = "POST",
                apPath = "/0/private/Balance",
                apDoSign = true
            };
            string retJson = await doApi_Base(p, _dbCon);

            var ret = JsonSerializer.Deserialize<KrakenEnvelope<Dictionary<string, string>>?>(retJson);

            return ret;
        }

        public async Task<KrakenEnvelope<KrakenTradesHistoryResult>?> doApi_TradesHistoryAsync(dbContext _dbCon)
        {
            cApiParms p = new cApiParms
            {
                apMethod = "POST",
                apPath = "/0/private/TradesHistory",
                apDoSign = true
            };
            string retJson = await doApi_Base(p, _dbCon);

            var ret = JsonSerializer.Deserialize<KrakenEnvelope<KrakenTradesHistoryResult>?>(retJson);

            return ret;
        }

        public async Task<string> doApi_Base(cApiParms p, dbContext _dbCon)
        {
            var environment = _apiDetails!.apidet_api_url!.TrimEnd('/');     // e.g. https://api.kraken.com
            var url = environment + p.apPath;                                // /0/private/Balance

            // ----- form body (nonce only for Balance) -----
            var nonce = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            var form = new List<KeyValuePair<string, string>> { new("nonce", nonce) };
            var formBody = new FormUrlEncodedContent(form);
            var postDataStr = await formBody.ReadAsStringAsync();            // "nonce=172...123"

            // ----- signature -----
            // API-Sign = Base64( HMAC-SHA512( uriPath + SHA256(nonce + postdata), base64(secret) ) )
            var sig = KrakenSign(p.apPath, postDataStr, nonce, _apiDetails!.apidet_secret!); // SECRET here

            var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = formBody
            };

            if (p.apDoSign && !string.IsNullOrEmpty(_apiDetails!.apidet_key))
            {
                req.Headers.Add("API-Key", _apiDetails!.apidet_key!);            // KEY here
                req.Headers.Add("API-Sign", sig);
            }

            var client = _httpClientFactory.CreateClient();
            var resp = await client.SendAsync(req);
            var ret = await resp.Content.ReadAsStringAsync();

            // helpful error
            if (!resp.IsSuccessStatusCode) throw new HttpRequestException($"{(int)resp.StatusCode} {resp.ReasonPhrase}: {ret}");
            return ret;
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

        private string GetSignature(string privateKey, string data, string nonce, string path)
        {
            using var sha256 = SHA256.Create();
            var sha = sha256.ComputeHash(Encoding.UTF8.GetBytes(nonce + data));
            var pathBytes = Encoding.UTF8.GetBytes(path);
            var toSign = new byte[pathBytes.Length + sha.Length];
            Buffer.BlockCopy(pathBytes, 0, toSign, 0, pathBytes.Length);
            Buffer.BlockCopy(sha, 0, toSign, pathBytes.Length, sha.Length);
            return Sign(privateKey, toSign);
        }

        private string Sign(string privateKey, byte[] message)
        {
            using var hmac = new HMACSHA512(Convert.FromBase64String(privateKey));
            var digest = hmac.ComputeHash(message);
            return Convert.ToBase64String(digest);
        }

        private string GetNonce() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

        private string BuildQueryString(Dictionary<string, string> query)
            => string.Join("&", query.Select(kv =>
                $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

        // --- Model Classes ---

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

}