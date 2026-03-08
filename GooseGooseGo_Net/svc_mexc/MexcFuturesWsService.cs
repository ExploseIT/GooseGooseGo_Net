using GooseGooseGo_Net.svc_mexc;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GooseGooseGo_Net.ef
{
    public sealed class MexcFuturesWsService : BackgroundService
    {
        private readonly IPriceCache _prices;
        private readonly ILogger<MexcFuturesWsService> _log;
        
        private readonly Uri _uri = new("wss://https://api.mexc.com"); //private readonly Uri _uri = new("wss://contract.mexc.com/edge");
        private readonly HubConnection _hubConnection;

        public MexcFuturesWsService(IPriceCache prices, ILogger<MexcFuturesWsService> log, HubConnection hubConnection)
        {
            _prices = prices;
            _log = log;
            _hubConnection = hubConnection;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var symbols = new[] { "BTC_USDT", "HYPE_USDT", "AIA_USDT", "BCH_USDT" };

            if (_hubConnection.State == HubConnectionState.Disconnected)
            {
                await _hubConnection.StartAsync(stoppingToken);
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                using var ws = new ClientWebSocket();
                try
                {
                    _log.LogInformation("Connecting to Mexc WebSocket...");
                    await ws.ConnectAsync(_uri, stoppingToken);
                    _log.LogInformation("Connected.");

                    // Subscribe to tickers
                    foreach (var symbol in symbols)
                    {
                        var subMsg = new
                        {
                            method = "sub.ticker",
                            param = new { symbol }
                        };
                        var subJson = JsonSerializer.Serialize(subMsg);
                        await ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(subJson)), WebSocketMessageType.Text, true, stoppingToken);
                        _log.LogInformation($"Subscribed to {symbol}");
                    }

                    // Start ping task
                    var pingTask = Task.Run(async () =>
                    {
                        while (ws.State == WebSocketState.Open)
                        {
                            try
                            {
                                var pingMsg = new { method = "ping" };
                                var pingJson = JsonSerializer.Serialize(pingMsg);
                                await ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(pingJson)), WebSocketMessageType.Text, true, stoppingToken);
                                await Task.Delay(15000, stoppingToken); // Every 15 seconds
                            }
                            catch (Exception ex)
                            {
                                _log.LogError(ex, "Ping error");
                                break;
                            }
                        }
                    }, stoppingToken);

                    // Receive loop
                    var buffer = new byte[4096];
                    var received = new List<byte>();
                    while (ws.State == WebSocketState.Open && !stoppingToken.IsCancellationRequested)
                    {
                        var segment = new ArraySegment<byte>(buffer);
                        var result = await ws.ReceiveAsync(segment, stoppingToken);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            _log.LogWarning("WebSocket closed by server.");
                            break;
                        }

                        received.AddRange(buffer.Take(result.Count));

                        if (result.EndOfMessage)
                        {
                            var message = Encoding.UTF8.GetString(received.ToArray());
                            received.Clear();

                            try
                            {
                                var jsonDoc = JsonDocument.Parse(message);
                                var channel = jsonDoc.RootElement.GetProperty("channel").GetString();

                                if (channel == "push.ticker")
                                {
                                    var data = jsonDoc.RootElement.GetProperty("data");
                                    var symbol = jsonDoc.RootElement.GetProperty("symbol").GetString();

                                    var snapshot = new MexcWsTickerSnapshot(
                                        Last: data.TryGetProperty("lastPrice", out var lp) && lp.ValueKind != JsonValueKind.Null ? lp.GetDecimal() : null,
                                        Bid: data.TryGetProperty("bid1", out var b) && b.ValueKind != JsonValueKind.Null ? b.GetDecimal() : null,
                                        Ask: data.TryGetProperty("ask1", out var a) && a.ValueKind != JsonValueKind.Null ? a.GetDecimal() : null,
                                        High24h: data.TryGetProperty("high24Price", out var h) && h.ValueKind != JsonValueKind.Null ? h.GetDecimal() : null,
                                        Low24h: data.TryGetProperty("lower24Price", out var l) && l.ValueKind != JsonValueKind.Null ? l.GetDecimal() : null,
                                        Vol24: data.TryGetProperty("volume24", out var v) && v.ValueKind != JsonValueKind.Null ? v.GetDecimal() : null, // contracts traded
                                        Amt24: null, // Not provided in API; could calculate if contract size known
                                        TsMs: data.TryGetProperty("timestamp", out var ts) && ts.ValueKind != JsonValueKind.Null ? ts.GetInt64() : 0
                                    );

                                    _prices.Upsert(symbol, _ => snapshot);
                                    _log.LogDebug($"Updated price for {symbol}");

                                    decimal? variation24h = null;
                                    if (snapshot.Low24h.HasValue && snapshot.Low24h > 0 && snapshot.High24h.HasValue)
                                    {
                                        variation24h = ((snapshot.High24h - snapshot.Low24h) / snapshot.Low24h) * 100;
                                    }

                                    if (variation24h > 5)
                                    {
                                        await _hubConnection.InvokeAsync("BroadcastPriceUpdate", new
                                        {
                                            Symbol = symbol,
                                            LastPrice = snapshot.Last,
                                            Bid = snapshot.Bid,
                                            Ask = snapshot.Ask,
                                            High24h = snapshot.High24h,
                                            Low24h = snapshot.Low24h,
                                            Volume24h = snapshot.Vol24,
                                            QuoteVolume24h = snapshot.Amt24,
                                            Variation24hPercent = variation24h,
                                            TimestampMs = snapshot.TsMs
                                        }, stoppingToken);
                                    }
                                }
                                else if (channel == "pong")
                                {
                                    // Handle pong if needed
                                }
                            }
                            catch (Exception ex)
                            {
                                _log.LogError(ex, "Error parsing message: {message}", message);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "WebSocket error");
                }
                finally
                {
                    if (ws.State != WebSocketState.Closed)
                    {
                        try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", stoppingToken); } catch { }
                    }
                }

                // Reconnect delay
                if (!stoppingToken.IsCancellationRequested)
                {
                    _log.LogInformation("Reconnecting in 5 seconds...");
                    await Task.Delay(5000, stoppingToken);
                }
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await _hubConnection.StopAsync(cancellationToken);
            await base.StopAsync(cancellationToken);
        }
    }
}
