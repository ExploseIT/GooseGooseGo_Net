namespace GooseGooseGo_Net.ef
{
    using System.Net.WebSockets;
    using System.Text;
    using System.Text.Json;

    public sealed class MexcFuturesWsService : BackgroundService
    {
        private readonly IPriceCache _prices;
        private readonly ILogger<MexcFuturesWsService> _log;
        private readonly Uri _uri = new("wss://contract.mexc.com/edge");

        public MexcFuturesWsService(IPriceCache prices, ILogger<MexcFuturesWsService> log)
        {
            _prices = prices; _log = log;
        }

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try { await RunOnce(ct); }
                catch (Exception ex) { _log.LogError(ex, "Futures WS crashed; restarting"); }
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
            }
        }

        private async Task RunOnce(CancellationToken ct)
        {
            using var ws = new ClientWebSocket();
            await ws.ConnectAsync(_uri, ct);
            _log.LogInformation("Connected MEXC Futures WS");

            // Subscribe to BTCUSDT perpetual (futures symbol uses underscore: BTC_USDT)
            // Ticker stream (includes lastPrice) – very light:
            await Send(ws, """{"method":"sub.tickers","param":{}}""", ct);
            // Or: individual trade prints (fastest last trade):
            await Send(ws, """{"method":"sub.deal","param":{"symbol":"BTC_USDT"}}""", ct);

            // keepalive pings
            _ = Task.Run(() => PingLoop(ws, ct), ct);

            var buf = new byte[64 * 1024];
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var res = await ws.ReceiveAsync(buf, ct);
                if (res.MessageType == WebSocketMessageType.Close) break;

                var json = Encoding.UTF8.GetString(buf, 0, res.Count);
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    if (!doc.RootElement.TryGetProperty("channel", out var chEl)) continue;
                    var ch = chEl.GetString() ?? "";

                    // Tickers push example: channel="push.tickers", data:[{symbol:"BTC_USDT", lastPrice: 93200.5, ...}], ts:...
                    if (ch.Equals("push.tickers", StringComparison.OrdinalIgnoreCase))
                    {
                        if (doc.RootElement.TryGetProperty("data", out var arr) && arr.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in arr.EnumerateArray())
                            {
                                var sym = item.GetProperty("symbol").GetString();  // e.g., "BTC_USDT"
                                if (sym is null) continue;

                                var symNorm = sym.Replace("_", "");  // BTCUSDT
                                var last = item.GetProperty("lastPrice").GetDecimal();
                                var ts = doc.RootElement.TryGetProperty("ts", out var tsEl) ? tsEl.GetInt64() : 0L;

                                _prices.Upsert(symNorm, old => old with
                                {
                                    Last = last,
                                    TsMs = ts
                                });
                            }
                        }
                    }


                    // Trades push example: channel="push.deal", data:{ symbol:"BTC_USDT", p: "93210.0", ... }, ts:...
                    if (ch.Equals("push.deal", StringComparison.OrdinalIgnoreCase))
                    {
                        if (doc.RootElement.TryGetProperty("data", out var d) && d.TryGetProperty("symbol", out var s))
                        {
                            var symRaw = s.GetString() ?? "BTC_USDT";
                            var sym = symRaw.Replace("_", "");
                            var px = decimal.Parse(d.GetProperty("p").GetString()!,
                                    System.Globalization.CultureInfo.InvariantCulture);
                            var ts = doc.RootElement.TryGetProperty("ts", out var tsEl) ? tsEl.GetInt64() : 0L;

                            _prices.Upsert(sym, old => old with
                            {
                                Last = px,
                                TsMs = ts
                            });
                        }
                    }

                }
                catch
                {
                    // ignore malformed keepalive messages, etc.
                }
            }
        }

        private static async Task Send(ClientWebSocket ws, string s, CancellationToken ct) =>
            await ws.SendAsync(Encoding.UTF8.GetBytes(s), WebSocketMessageType.Text, true, ct);

        private static async Task PingLoop(ClientWebSocket ws, CancellationToken ct)
        {
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(15), ct);
                await Send(ws, """{"method":"ping"}""", ct); // server replies {"channel":"pong","data":...}
            }
        }
    }

}

