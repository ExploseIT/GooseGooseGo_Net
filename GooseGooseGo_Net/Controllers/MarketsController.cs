using GooseGooseGo_Net.Services;
using Microsoft.AspNetCore.Mvc;

namespace GooseGooseGo_Net.Controllers
{
    public class MarketsController : Controller
    {
        [HttpGet("/markets/ticker")]
        public async Task<IActionResult> Index()
        {
            var resp = await KrakenClient.Request(
                method: "GET",
                path: "/0/public/Ticker",
                environment: "https://api.kraken.com"
            );

            var json = await resp.Content.ReadAsStringAsync();
            // Return raw JSON (or pass to a ViewModel)
            return Content(json, "application/json");
        }
    }
}
