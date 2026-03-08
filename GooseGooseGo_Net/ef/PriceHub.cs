
using Microsoft.AspNetCore.SignalR;

namespace GooseGooseGo_Net.ef
{
    public class PriceHub : Hub
    {


    } // broadcast via Clients.All.SendAsync("price", symbol, price, ts)
}
