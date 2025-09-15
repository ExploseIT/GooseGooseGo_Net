using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GooseGooseGo_Net.Services
{
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using static System.Net.WebRequestMethods;

    namespace GooseGooseGo_Net.Services
    {
        public partial class CryptoComClient
        {
            private static readonly HttpClient Http = new HttpClient();

            public static async Task<HttpResponseMessage> Request(
                string methodName,                                  // "public/get-tickers" or "private/get-account-summary"
                Dictionary<string, object>? @params = null,
                IConfiguration conf = null!,
                string environment = "https://api.crypto.com/exchange/"
            )
            {
                var confSection = conf.GetSection("CRYPTOCOM_API");
                var apiKey = confSection["CRYPTOCOM_KEY"] ?? "";
                var apiSecret = confSection["CRYPTOCOM_SECRET"] ?? "";

                if (string.IsNullOrWhiteSpace(environment))
                    throw new ArgumentException("Environment (base URL) is required.", nameof(environment));

                methodName = methodName.Trim().TrimStart('/');
                var baseUrl = environment.TrimEnd('/') + "/v1/" + methodName;

                var isPrivate = methodName.StartsWith("private/", StringComparison.OrdinalIgnoreCase);
                var isPublic = methodName.StartsWith("public/", StringComparison.OrdinalIgnoreCase);

                HttpRequestMessage req;

                if (isPublic)
                {
                    // Build querystring (e.g., instrument_name=BTC_USDT). Empty/NULL => all tickers
                    var qs = (@params == null || @params.Count == 0)
                        ? ""
                        : "?" + string.Join("&", @params.Select(kv =>
                            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(Convert.ToString(kv.Value, System.Globalization.CultureInfo.InvariantCulture) ?? "")}"));

                    req = new HttpRequestMessage(HttpMethod.Get, baseUrl + qs);
                }
                else
                {
                    // ----- PRIVATE: POST JSON with signature -----
                    var id = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    var nonce = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                    var body = new Dictionary<string, object>
                    {
                        ["id"] = id,
                        ["method"] = methodName,
                        ["params"] = @params ?? new Dictionary<string, object>()
                    };

                    if (isPrivate)
                    {
                        if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiSecret))
                            throw new InvalidOperationException("API key/secret required for private call.");

                        body["api_key"] = apiKey;
                        body["nonce"] = nonce;

                        // sig = HMAC_SHA256_HEX(method + id + api_key + paramStr + nonce)
                        var paramStr = BuildParamString((Dictionary<string, object>)body["params"]);
                        var payload = $"{methodName}{id}{apiKey}{paramStr}{nonce}";
                        body["sig"] = SignHex(apiSecret, payload);
                    }

                    var json = JsonSerializer.Serialize(body, JsonOpts);
                    req = new HttpRequestMessage(HttpMethod.Post, baseUrl)
                    {
                        Content = new StringContent(json, Encoding.UTF8, "application/json")
                    };
                }

                var resp = await Http.SendAsync(req);

                if (!resp.IsSuccessStatusCode)
                {
                    var err = await resp.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase} for {req.RequestUri}\nResponse: {err}");
                }

                return resp;
            }


            // ---------- helpers ----------

            private static readonly JsonSerializerOptions JsonOpts = new()
            {
                PropertyNameCaseInsensitive = true
            };

            private static string SignHex(string secret, string message)
            {
                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
                var digest = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
                var sb = new StringBuilder(digest.Length * 2);
                foreach (var b in digest) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }

            /// <summary>
            /// v1 requires alphabetical key order; value concatenation.
            /// For list/array values, concatenate each item with no separators.
            /// </summary>
            private static string BuildParamString(Dictionary<string, object> dict)
            {
                if (dict == null || dict.Count == 0) return string.Empty;

                var sb = new StringBuilder();
                foreach (var kv in dict.OrderBy(k => k.Key, StringComparer.Ordinal))
                {
                    sb.Append(kv.Key);
                    switch (kv.Value)
                    {
                        case null:
                            break;
                        case IEnumerable<object> list:
                            foreach (var item in list)
                                sb.Append(Convert.ToString(item, System.Globalization.CultureInfo.InvariantCulture));
                            break;
                        default:
                            sb.Append(Convert.ToString(kv.Value, System.Globalization.CultureInfo.InvariantCulture));
                            break;
                    }
                }
                return sb.ToString();
            }


            // ---------- PUBLIC METHOD (convenience) ----------
            // Builds exactly your example: one LIMIT + one STOP_LIMIT on the same instrument.
            public static async Task<CryptoComEnvelope<CreateOrderListResult>> CreateOrderListAsync(
                string instrumentName,
                string limitPrice, string limitQty,
                string stopLimitPrice, string stopLimitQty, string triggerPrice,
                IConfiguration conf,
                string contingencyType = "LIST",
                string environment = "https://api.crypto.com/exchange/"
            )
            {
                var orders = new List<Dictionary<string, object?>>
            {
                new()
                {
                    ["instrument_name"] = instrumentName,
                    ["side"]            = "BUY",
                    ["type"]            = "LIMIT",
                    ["price"]           = limitPrice,
                    ["quantity"]        = limitQty
                },
                new()
                {
                    ["instrument_name"] = instrumentName,
                    ["side"]            = "BUY",
                    ["type"]            = "STOP_LIMIT",
                    ["price"]           = stopLimitPrice,
                    ["quantity"]        = stopLimitQty,
                    ["trigger_price"]   = triggerPrice
                }
            };

                return await CreateOrderListAsync(orders, conf, contingencyType, environment);
            }

            // ---------- PUBLIC METHOD (generic) ----------
            // Accepts any order list shape supported by Crypto.com v1
            public static async Task<CryptoComEnvelope<CreateOrderListResult>> CreateOrderListAsync(
                List<Dictionary<string, object?>> orderList,
                IConfiguration conf,
                string contingencyType = "LIST",
                string environment = "https://api.crypto.com/exchange/"
            )
            {
                var @params = new Dictionary<string, object?>
                {
                    ["contingency_type"] = contingencyType,
                    ["order_list"] = orderList
                };

                using var resp = await Request(
                    methodName: "private/create-order-list",
                    @params: @params!,
                    conf: conf,
                    environment: environment
                );

                resp.EnsureSuccessStatusCode();

                using var stream = await resp.Content.ReadAsStreamAsync();
                var env = await JsonSerializer.DeserializeAsync<CryptoComEnvelope<CreateOrderListResult>>(stream, JsonOpts)
                          ?? throw new InvalidOperationException("Empty response from Crypto.com");

                if (env.Code != 0)
                    throw new InvalidOperationException($"Crypto.com error (code {env.Code}) in create-order-list");

                return env;
            }



            public static async Task<CryptoComTickerEnvelope> GetTickersAsync(
            IConfiguration conf,
            string? instrumentName = null,
            string environment = "https://api.crypto.com/exchange/"
            )
            {
                var @params = string.IsNullOrEmpty(instrumentName)
                    ? new Dictionary<string, object>()
                    : new Dictionary<string, object> { ["instrument_name"] = instrumentName };

                using var resp = await Request(
                    methodName: "public/get-tickers",
                    @params: @params,
                    conf: conf,
                    environment: environment
                );

                resp.EnsureSuccessStatusCode();

                using var stream = await resp.Content.ReadAsStreamAsync();
                var env = await JsonSerializer.DeserializeAsync<CryptoComTickerEnvelope>(stream, JsonOpts)
                          ?? throw new InvalidOperationException("Empty response from Crypto.com");

                if (env.Code != 0)
                    throw new InvalidOperationException($"Crypto.com error (code {env.Code}) in get-ticker");

                return env;
            }
        }
    }


    // Generic v1 envelope: { id, method, code, result }
    public sealed class CryptoComEnvelope<T>
    {
        [JsonPropertyName("id")] public long Id { get; set; }
        [JsonPropertyName("method")] public string Method { get; set; } = "";
        [JsonPropertyName("code")] public int Code { get; set; }
        [JsonPropertyName("result")] public T Result { get; set; } = default!;
    }

    public sealed class CryptoComTickerEnvelope
    {
        [JsonPropertyName("id")] public long Id { get; set; }
        [JsonPropertyName("method")] public string Method { get; set; } = "";
        [JsonPropertyName("code")] public int Code { get; set; }
        [JsonPropertyName("result")] public CryptoComTickerResult Result { get; set; } = new();
    }

    public sealed class CryptoComTickerResult
    {
        [JsonPropertyName("data")] public List<CryptoComTickerEntry> Data { get; set; } = new();
    }

    public sealed class CryptoComTickerEntry
    {
        // instrument_name, e.g., "BTC_USDT"
        [JsonPropertyName("i")] public string Instrument { get; set; } = "";

        // best ask
        [JsonPropertyName("a")] public string Ask { get; set; } = "";

        // best bid
        [JsonPropertyName("b")] public string Bid { get; set; } = "";

        // last price
        [JsonPropertyName("k")] public string Last { get; set; } = "";

        // timestamp (ms)
        [JsonPropertyName("t")] public long Timestamp { get; set; }
    }

    // Result for private/create-order-list
    public sealed class CreateOrderListResult
    {
        // Commonly returned fields (names per Crypto.com docs):
        // Some tenants return "order_list_id", some return "order_ids"/"order_list".
        [JsonPropertyName("order_list_id")] public string? OrderListId { get; set; }

        // If API returns an array of created order ids
        [JsonPropertyName("order_ids")] public List<string> OrderIds { get; set; } = new();

        // If API returns richer objects for each order in the list
        [JsonPropertyName("order_list")] public List<JsonElement> OrderList { get; set; } = new();

        // Keep any extra fields the API might add without breaking your deserialization
        [JsonExtensionData] public Dictionary<string, JsonElement> Extra { get; set; } = new();
    }


    public sealed class CryptoComV1TickerResult
    {
        [JsonPropertyName("data")] public List<CryptoComV1TickerEntry> Data { get; set; } = new();
    }

    public sealed class CryptoComV1TickerEntry
    {
        // instrument_name
        [JsonPropertyName("i")] public string Instrument { get; set; } = "";

        // best ask
        [JsonPropertyName("a")] public string Ask { get; set; } = "";

        // best bid
        [JsonPropertyName("b")] public string Bid { get; set; } = "";

        // last traded price
        [JsonPropertyName("k")] public string Last { get; set; } = "";

        // timestamp (ms)
        [JsonPropertyName("t")] public long Timestamp { get; set; }
    }
}
