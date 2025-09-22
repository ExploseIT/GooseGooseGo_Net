

using Azure;
using Azure.Core;
using GooseGooseGo_Net.ef;
using GooseGooseGo_Net.Models;
using GooseGooseGo_Net.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using static System.Net.WebRequestMethods;

namespace GooseGooseGo_Net.ef
{
    public class ent_kraken
    {

        private dbContext? dbCon { get; } = null;
        private IConfiguration _conf { get; } = null!;
        private ILogger _logger { get; } = null!;

        private cKrakenApiDetails? _apiDetails = null!;
        private string _encryptionKey = null!;
        Exception? exc = null!;

        public ent_kraken()
        {
        }

        public string doApiDetailsEncrypt()
        {
            string ret = "";
            var _e_settings = new ent_setting(dbCon!, _logger);
            var set_kraken_api = _e_settings.doSettingsReadByName("KRAKEN_API");
            if (set_kraken_api == null)
            {
                var cs = _conf.GetSection("KRAKEN_API");
                string _apiUrl = cs.GetValue<string>("KRAKEN_API_URL")!;
                string _apiKey = cs.GetValue<string>("KRAKEN_KEY")!;
                string _apiSecret = cs.GetValue<string>("KRAKEN_SECRET")!;

                var cKrakenApiDetails = new cKrakenApiDetails
                {
                    apidet_api_url = _apiUrl,
                    apidet_key = _apiKey,
                    apidet_secret = _apiSecret
                };
                string json = JsonSerializer.Serialize(cKrakenApiDetails);
                ret = mEncryption.EncryptString(json, _encryptionKey);
                _e_settings.doSettingsInsertByName("KRAKEN_API",ret, "Encrypted Kraken API details");
            }
            return ret;
        }
        public cKrakenApiDetails doApiDetailsDecrypt()
        {
            cKrakenApiDetails ret = null!;
            var _e_settings = new ent_setting(dbCon!, _logger);
            var set_kraken_api = _e_settings.doSettingsReadByName("KRAKEN_API");
            if (set_kraken_api != null)
            { 
                var retEncrypt = _e_settings.doSettingsReadByName("KRAKEN_API");
                var retJson = mEncryption.DecryptString(retEncrypt!.setValue, _encryptionKey);
                ret = JsonSerializer.Deserialize<cKrakenApiDetails>(retJson)!;
            }

            return ret;
        }

        public ent_kraken(IConfiguration conf, dbContext dbCon, ILogger logger)
        {
            this._conf = conf;
            this.dbCon = dbCon;
            this._logger = logger;

            _encryptionKey = conf.GetSection("Encryption").GetValue<string>("EncryptionKey")!;
            _apiDetails = doApiDetailsDecrypt();
        }

        public async Task<KrakenEnvelope<Dictionary<string, KrakenTickerEntry>>?> doApi_TickerListAsync()
        {
            cKrakenAPIParms p = new cKrakenAPIParms
            {
                apMethod = "GET",
                apPath = "/0/public/Ticker"
            };
            string retJson = await doApi_Base(p);

            var ret = JsonSerializer.Deserialize<KrakenEnvelope<Dictionary<string, KrakenTickerEntry>>>(retJson);

            return ret;
        }

        public async Task<string> doApi_Base(cKrakenAPIParms p)
        {
            string ret = null!;
            Dictionary<string, string>? query = null;
            Dictionary<string, object>? body = null;
            string environment = _apiDetails.apidet_api_url;
            if (string.IsNullOrWhiteSpace(environment))
                throw new ArgumentException("Environment (base URL) is required.", nameof(environment));

            // Build URL + query
            var baseUri = environment.TrimEnd('/');
            var url = new StringBuilder(baseUri).Append(p.apPath).ToString();

            string queryStr = "";
            if (query is { Count: > 0 })
            {
                queryStr = BuildQueryString(query);
                url += "?" + queryStr;
            }

            // Nonce/body handling for private calls
            string nonce = "";
            if (!string.IsNullOrEmpty(_apiDetails.apidet_key))
            {
                body ??= new Dictionary<string, object>(StringComparer.Ordinal);
                if (!body.TryGetValue("nonce", out _))
                {
                    nonce = GetNonce();
                    body["nonce"] = nonce;
                }
                else
                {
                    nonce = Convert.ToString(body["nonce"]) ?? GetNonce();
                }
            }

            string bodyStr = "";
            var request = new HttpRequestMessage(new HttpMethod(p.apMethod), url);

            if (body is { Count: > 0 })
            {
                bodyStr = JsonSerializer.Serialize(body);
                request.Content = new StringContent(bodyStr, Encoding.UTF8, "application/json");
            }

            // Headers (auth if provided)
            if (!string.IsNullOrEmpty(_apiDetails.apidet_key))
            {
                request.Headers.Add("API-Key", _apiDetails.apidet_key);

                // Signature over path + sha256(nonce + (queryStr + bodyStr))
                var sig = GetSignature(_apiDetails.apidet_key, data: queryStr + bodyStr, nonce: nonce, path: p.apPath);
                request.Headers.Add("API-Sign", sig);
            }

            var client = new HttpClient();
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            string result = await response.Content.ReadAsStringAsync();

            ret = result;

            return ret;
        }

        private string GetSignature(string privateKey, string data, string nonce, string path)
        {
            // message = path + SHA256(nonce + data)
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
            // HMAC-SHA512 with base64-decoded secret, then base64-encode the digest
            using var hmac = new HMACSHA512(Convert.FromBase64String(privateKey));
            var digest = hmac.ComputeHash(message);
            return Convert.ToBase64String(digest);
        }
        private string GetNonce() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        
        private string BuildQueryString(Dictionary<string, string> query)
            => string.Join("&", query.Select(kv =>
                $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        /*
        public bool doAPI_AddOrder()
        {
            curl - L 'https://api.kraken.com/0/private/AddOrder' \
-H 'Content-Type: application/json' \
-H 'Accept: application/json' \
-H 'API-Key: <API-Key>' \
-H 'API-Sign: <API-Sign>' \
-d '{
  "nonce": 163245617,
  "ordertype": "limit",
  "type": "buy",
  "volume": "1.25",
  "pair": "XBTUSD",
  "price": "27500",
  "cl_ord_id": "6d1b345e-2821-40e2-ad83-4ecb18a06876"
}'
        }
        */


        public cKrakenAssetInfo doKrakenGetNextId()
        {
            cKrakenAssetInfo ret = new cKrakenAssetInfo();
            try
            {
                SqlParameter[] lParams = { };
                string sp = "spKrakenAssetInfoNextId";

                var retSP = this.dbCon?.lKrakenAssetInfo.FromSqlRaw(sp, lParams).AsEnumerable();

                ret = retSP?.FirstOrDefault()!;
            }
            catch (Exception ex)
            {
                exc = ex;
            }

            return ret;
        }

        public cKraken? doKrakenUpdateById(cKraken p)
        {
            cKraken? ret = null;

            try
            {
                SqlParameter[] lParams = {
                new SqlParameter("@kaId", SqlDbType.Int, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, p.kaId)
                , new SqlParameter("@kaIndex", SqlDbType.Int, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, p.kaIndex)
                , new SqlParameter("@kaPair", SqlDbType.NVarChar, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, p.kaPair)
                , new SqlParameter("@kaLastTrade", SqlDbType.Decimal, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, p.kaLastTrade)
                , new SqlParameter("@kaOpen", SqlDbType.Decimal, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, p.kaOpen)
                , new SqlParameter("@kaBid", SqlDbType.Decimal, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, p.kaBid)
                , new SqlParameter("@kaAsk", SqlDbType.Decimal, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, p.kaAsk)
                , new SqlParameter("@kaHigh24h", SqlDbType.Decimal, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, p.kaHigh24h)
                , new SqlParameter("@kaLow24h", SqlDbType.Decimal, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, p.kaLow24h)
                , new SqlParameter("@kaVolume24h", SqlDbType.NVarChar, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, p.kaVolume24h)
                , new SqlParameter("@kaRetrievedAt", SqlDbType.DateTime, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, p. kaRetrievedAt.ToLocalTime())

            };

                string sp = "spKrakenUpdateById @kaId,@kaIndex,@kaPair,@kaLastTrade,@kaOpen,@kaBid,@kaAsk,@kaHigh24h,@kaLow24h,@kaVolume24h,@kaRetrievedAt";

                var retSP = this.dbCon?.lKraken.FromSqlRaw(sp, lParams).AsEnumerable();

                ret = retSP?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                exc = ex;
            }

            return ret;
        }



        public List<cKrakenPercentageSwing>? doKrakenPercentageSwingList(cKrakenPercentageSwingParms p)
        {
            List<cKrakenPercentageSwing> ret = new List<cKrakenPercentageSwing>();

            try
            {
                SqlParameter[] lParams = {
                new SqlParameter("@kapsMinSwing", SqlDbType.Decimal, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, p.kapsMinSwing)
                , new SqlParameter("@kapsPeriodValue", SqlDbType.Int, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, p.kapsPeriodValue)
                , new SqlParameter("@kapsPeriodUnit", SqlDbType.NVarChar, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, p.kapsPeriodUnit)
                , new SqlParameter("@kapsPeriodOffset", SqlDbType.Int, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, p.kapsPeriodOffset)             

            };

                string sp = "spKrakenRollingPercentSwing @kapsMinSwing, @kapsPeriodValue, @kapsPeriodUnit, @kapsPeriodOffset";

                var retSP = this.dbCon?.lKrakenPercentageSwing.FromSqlRaw(sp, lParams).AsEnumerable();

                ret = retSP?.ToList()!;
            }
            catch (Exception ex)
            {
                exc = ex;
            }

            return ret;
        }

    }

    public class cKrakenAssetInfo
    {
        [Key]
        public int kaiId { get; set; }
        public DateTime kaiDT { get; set; }
    }


    public class cKraken
    {
        [Key]
        public int kaId { get; set; }
        public int kaIndex { get; set; }
        public string kaPair { get; set; } = "";

        [Precision(18, 8)]
        public decimal kaLastTrade { get; set; }

        [Precision(18, 8)]
        public decimal? kaOpen { get; set; }

        [Precision(18, 8)]
        public decimal? kaBid { get; set; }

        [Precision(18, 8)]
        public decimal? kaAsk { get; set; }

        [Precision(18, 8)]
        public decimal? kaHigh24h { get; set; }

        [Precision(18, 8)]
        public decimal? kaLow24h { get; set; }

        public string? kaVolume24h { get; set; }
        public DateTime kaRetrievedAt { get; set; }
    }

    public class cKrakenPercentageSwingParms
    {
        public decimal kapsMinSwing { get; set; } = 0.0M;
        public int kapsPeriodValue { get; set; } = 0;
        public string kapsPeriodUnit { get; set; } = "";
        public int kapsPeriodOffset { get; set; } = 0;
    }


    public class cKrakenPercentageSwing
    {
        [Key]
        public string kapsPair { get; set; } = "";
        [Precision(18, 4)]
        public decimal kapsStartTrade { get; set; } = 0.0M;
        [Precision(18, 4)]
        public decimal kapsEndTrade { get; set; } = 0.0M;
        [Precision(18, 4)]
        public decimal kapsTradeDiffPercent { get; set; } = 0.0M;
        [Precision(18, 4)]
        public decimal kapsTradeDiff { get; set; } = 0.0M;
        [Precision(18, 4)]
        public decimal kapsTradeDiffAbs { get; set; } = 0.0M;
        public string kapsStartVolume { get; set; } = "";
        public string kapsEndVolume { get; set; } = "";
        public DateTime kapsStartRetrievedAt { get; set; }
        public DateTime kapsEndRetrievedAt { get; set; }
    }


    public class cKrakenAPIParms
    {
        public string apMethod { get; set; } = "GET";
        public string apPath { get; set; } = "";
    }


    // Generic Kraken envelope: { "error":[], "result":{...} }
    public sealed record KrakenEnvelope<T>(
        [property: JsonPropertyName("error")] List<string> Error,
        [property: JsonPropertyName("result")] T? Result
    );

    // ----- Ticker -----

    public sealed class KrakenTickerEntry
    {
        // Best ask [price, whole lot volume, lot volume]
        [JsonPropertyName("a")] public string[]? Ask { get; set; }
        // Best bid [price, whole lot volume, lot volume]
        [JsonPropertyName("b")] public string[]? Bid { get; set; }
        // Last trade closed [price, lot volume]
        [JsonPropertyName("c")] public string[]? LastTrade { get; set; }
        // Volume [today, last 24 hours]
        [JsonPropertyName("v")] public string[]? Volume { get; set; }
        // Volume weighted average price [today, last 24 hours]
        [JsonPropertyName("p")] public string[]? Vwap { get; set; }
        // Number of trades [today, last 24 hours]
        [JsonPropertyName("t")] public int[]? Trades { get; set; }
        // Low [today, last 24 hours]
        [JsonPropertyName("l")] public string[]? Low { get; set; }
        // High [today, last 24 hours]
        [JsonPropertyName("h")] public string[]? High { get; set; }
        // Today’s opening price
        [JsonPropertyName("o")] public string? Open { get; set; }
    }

    // View-friendly ticker row
    public sealed record TickerRow(
        string Pair,          // e.g. "XBTUSD"
        decimal Last,         // last trade price
        decimal Open,         // today's open
        decimal ChangePct,    // (Last-Open)/Open*100
        decimal Bid,
        decimal Ask,
        decimal High24h,
        decimal Low24h,
        string Volume24h
    );

    // ----- OHLC -----

    // Single OHLC candle from Kraken: [time, open, high, low, close, vwap, volume, count]
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

    


    public class  cKrakenApiDetails
    {
        public string apidet_api_url { get; set; } = "";
        public string apidet_key { get; set; } = "";
        public string apidet_secret { get; set; } = "";
    }
}
