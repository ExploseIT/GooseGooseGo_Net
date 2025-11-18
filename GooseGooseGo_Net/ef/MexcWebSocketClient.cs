namespace GooseGooseGo_Net.ef
{
    using Microsoft.AspNetCore.SignalR;
    using System;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    public class MexcWebSocketClient
    {
        private readonly Uri _uri = new("wss://wbs.mexc.com/ws");
        private readonly ClientWebSocket _socket = new();

        public async Task ConnectAsync(string symbol = "BTCUSDT")
        {
            await _socket.ConnectAsync(_uri, CancellationToken.None);
            Console.WriteLine("✅ Connected to MEXC WebSocket");

            var subMsg = $"{{\"method\":\"SUBSCRIPTION\",\"params\":[\"spot@public.deals.v3.api@{symbol}\"]}}";
            await SendAsync(subMsg);

            _ = Task.Run(ReceiveLoopAsync);
        }

        private async Task SendAsync(string message)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            await _socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private async Task ReceiveLoopAsync()
        {
            var buffer = new byte[8192];
            while (_socket.State == WebSocketState.Open)
            {
                var result = await _socket.ReceiveAsync(buffer, CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                Console.WriteLine($"📊 {json}");
                // TODO: parse and feed into GGG price engine
            }
        }
    }

    public class PriceHub : Hub { } // broadcast via Clients.All.SendAsync("price", symbol, price, ts)

}
