
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace GooseGooseGo_Net.svc_mexc
{
    public interface IPriceCache
    {
        bool TryGet(string symbol, out MexcWsTickerSnapshot snap);
        void Upsert(string symbol, Func<MexcWsTickerSnapshot, MexcWsTickerSnapshot> updater);
    }
}
