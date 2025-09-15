
// Program.cs
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace GooseGooseGo_Net.Services
{
    public class KrakenClient
    {

        // Reuse a single HttpClient instance
        private static readonly HttpClient Http = new HttpClient();


        /// <summary>
        /// Generic request with optional Kraken auth headers.
        /// </summary>
        public static async Task<HttpResponseMessage> Request(
            string method = "GET",
            string path = "",
            Dictionary<string, string>? query = null,
            Dictionary<string, object>? body = null,
            IConfiguration conf = null!,

            string environment = ""
        )
        {
            var confSection = conf.GetSection("KRAKEN_API");
            var keyPublic = confSection["KRAKEN_KEY"]!;
            var keySecret = confSection["KRAKEN_SECRET"]!;

            if (string.IsNullOrWhiteSpace(environment))
                throw new ArgumentException("Environment (base URL) is required.", nameof(environment));

            // Build URL + query
            var baseUri = environment.TrimEnd('/');
            var url = new StringBuilder(baseUri).Append(path).ToString();

            string queryStr = "";
            if (query is { Count: > 0 })
            {
                queryStr = BuildQueryString(query);
                url += "?" + queryStr;
            }

            // Nonce/body handling for private calls
            string nonce = "";
            if (!string.IsNullOrEmpty(keyPublic))
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
            var req = new HttpRequestMessage(new HttpMethod(method), url);

            if (body is { Count: > 0 })
            {
                bodyStr = JsonSerializer.Serialize(body);
                req.Content = new StringContent(bodyStr, Encoding.UTF8, "application/json");
            }

            // Headers (auth if provided)
            if (!string.IsNullOrEmpty(keyPublic))
            {
                req.Headers.Add("API-Key", keyPublic);

                // Signature over path + sha256(nonce + (queryStr + bodyStr))
                var sig = GetSignature(keyPublic, data: queryStr + bodyStr, nonce: nonce, path: path);
                req.Headers.Add("API-Sign", sig);
            }

            return await Http.SendAsync(req);
        }

        private static string GetNonce() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

        private static string GetSignature(string privateKey, string data, string nonce, string path)
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

        private static string Sign(string privateKey, byte[] message)
        {
            // HMAC-SHA512 with base64-decoded secret, then base64-encode the digest
            using var hmac = new HMACSHA512(Convert.FromBase64String(privateKey));
            var digest = hmac.ComputeHash(message);
            return Convert.ToBase64String(digest);
        }

        private static string BuildQueryString(Dictionary<string, string> query)
            => string.Join("&", query.Select(kv =>
                $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
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

}
