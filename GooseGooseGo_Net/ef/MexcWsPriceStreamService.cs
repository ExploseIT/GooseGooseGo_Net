namespace GooseGooseGo_Net.ef
{
    using GooseGooseGo_Net.Models;
    using Microsoft.AspNetCore.SignalR;
    using System.Net.WebSockets;
    using System.Text;
    using System.Text.Json;

    public class MexcWsPriceStreamService : BackgroundService
    {
        private readonly ILogger<MexcWsPriceStreamService> _log;
        private readonly IServiceProvider _sp;
        private readonly IPriceCache _prices;
        private ClientWebSocket? _ws;
        private readonly Uri _uri = new("wss://wbs-api.mexc.com/ws"); // per new docs
        private readonly TimeSpan _maxSession = TimeSpan.FromHours(23.5); // force daily rotate
        private readonly TimeSpan _pingEvery = TimeSpan.FromSeconds(20);
        private readonly HashSet<string> _subs = new(StringComparer.OrdinalIgnoreCase);

        public MexcWsPriceStreamService(ILogger<MexcWsPriceStreamService> log, IServiceProvider sp, IPriceCache prices)
        { _log = log; _sp = sp; _prices = prices; }

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try { await RunOnce(ct); }
                catch (Exception ex) { _log.LogError(ex, "WS loop crashed"); }
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
        }

        private async Task RunOnce(CancellationToken ct)
        {
            using var scope = _sp.CreateScope();
            var hub = scope.ServiceProvider.GetRequiredService<IHubContext<PriceHub>>();

            _ws = new ClientWebSocket();
            await _ws.ConnectAsync(_uri, ct);
            _log.LogInformation("Connected MEXC WS");

            var connectedAt = DateTime.UtcNow;

            // Initial subscriptions based on balances
            await RefreshSubscriptionsAsync(ct);

            var recv = Task.Run(() => ReceiveLoopAsync(_ws, hub, ct), ct);
            var keepAlive = Task.Run(() => KeepAliveLoopAsync(_ws, ct), ct);
            var resub = Task.Run(() => PeriodicResubscribeAsync(ct), ct);

            // rotate session before 24h hard cap
            while (!ct.IsCancellationRequested && DateTime.UtcNow - connectedAt < _maxSession &&
                   _ws.State == WebSocketState.Open)
            {
                await Task.Delay(1000, ct);
            }

            try { _ws.Abort(); _ws.Dispose(); } catch { /* ignore */ }
        }

        private async Task ReceiveLoopAsync(ClientWebSocket ws, IHubContext<PriceHub> hub, CancellationToken ct)
        {
            var buf = new byte[64 * 1024];
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var res = await ws.ReceiveAsync(buf, ct);
                if (res.MessageType == WebSocketMessageType.Close) break;

                var json = Encoding.UTF8.GetString(buf, 0, res.Count);

                // Example trade push structure (aggre.deals)
                // {
                //   "channel": "spot@public.aggre.deals.v3.api.pb@100ms@BTCUSDT",
                //   "publicdeals": { "dealsList":[{"price":"93220.00","quantity":"0.1","tradetype":2,"time":1736409765051}], "eventtype":"..." }
                // }
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("channel", out var chEl)) continue;
                var channel = chEl.GetString() ?? "";
                var parts = channel.Split('@');
                var symbol = parts.Last(); // ...@BTCUSDT

                // pick the *last* trade price in this batch
                if (doc.RootElement.TryGetProperty("publicdeals", out var deals)
                    && deals.TryGetProperty("dealsList", out var list) && list.GetArrayLength() > 0)
                {
                    var last = list[list.GetArrayLength() - 1];
                    var priceStr = last.GetProperty("price").GetString() ?? "0";
                    if (decimal.TryParse(priceStr, System.Globalization.NumberStyles.Any,
                                         System.Globalization.CultureInfo.InvariantCulture, out var px))
                    {
                        var ts = last.GetProperty("time").GetInt64();
                        _prices.Upsert(symbol, old => old with { Last = px, TsMs = ts });
                        await hub.Clients.All.SendAsync("price", symbol, px, ts, ct);
                    }
                }
            }
        }

        private async Task KeepAliveLoopAsync(ClientWebSocket ws, CancellationToken ct)
        {
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                await Task.Delay(_pingEvery, ct);
                var ping = "{\"method\":\"PING\"}";
                await ws.SendAsync(Encoding.UTF8.GetBytes(ping), WebSocketMessageType.Text, true, ct);
            }
        }

        private async Task PeriodicResubscribeAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _ws?.State == WebSocketState.Open)
            {
                await Task.Delay(TimeSpan.FromMinutes(5), ct);
                await RefreshSubscriptionsAsync(ct);
            }
        }

        private async Task RefreshSubscriptionsAsync(CancellationToken ct)
        {
            // Read balances with your existing code to compute wanted symbols
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<dbContext>();
            var mexc = scope.ServiceProvider.GetRequiredService<ent_mexc>();

            var acct = await mexc.doApi_AccountsAsync(db);
            var assets = acct?.balances ?? new List<cMexcAccountEntry>();

            IEnumerable<string> Candidates(string a) => new[] { $"{a}USDT", $"{a}USDC", $"{a}USD" };
            var wanted = assets
                .Where(a => a.getQuantity() > 0m)
                .SelectMany(a => Candidates(a.asset))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // prefer USDT first if multiple exist
            wanted = wanted
                .GroupBy(s => s[..^4], StringComparer.OrdinalIgnoreCase) // group by asset (strip quote)
                .Select(g => g.OrderByDescending(s => s.EndsWith("USDT", StringComparison.OrdinalIgnoreCase)
                                                  ? 2 : s.EndsWith("USDC", StringComparison.OrdinalIgnoreCase) ? 1 : 0).First())
                .ToList();

            var toAdd = wanted.Except(_subs, StringComparer.OrdinalIgnoreCase).ToList();
            var toRemove = _subs.Except(wanted, StringComparer.OrdinalIgnoreCase).ToList();

            if (_ws?.State == WebSocketState.Open)
            {
                if (toRemove.Count > 0)
                {
                    var unsub = new { method = "UNSUBSCRIPTION", @params = toRemove.Select(s => $"spot@public.aggre.deals.v3.api.pb@100ms@{s}").ToArray() };
                    var json = JsonSerializer.Serialize(unsub);
                    await _ws.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, ct);
                    foreach (var s in toRemove) _subs.Remove(s);
                }
                if (toAdd.Count > 0)
                {
                    var sub = new { method = "SUBSCRIPTION", @params = toAdd.Select(s => $"spot@public.aggre.deals.v3.api.pb@100ms@{s}").ToArray() };
                    var json = JsonSerializer.Serialize(sub);
                    await _ws.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, ct);
                    foreach (var s in toAdd) _subs.Add(s);
                }
            }
        }
    }

}
