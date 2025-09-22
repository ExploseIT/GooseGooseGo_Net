

using GooseGooseGo_Net.ef;
using GooseGooseGo_Net.Models;
using GooseGooseGo_Net.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;

namespace GooseGooseGo_Net.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private IConfiguration _conf;
        private IWebHostEnvironment _env;
        private dbContext _dbCon;

        public HomeController(ILogger<HomeController> logger, IConfiguration conf, IWebHostEnvironment env, dbContext dbCon)
        {
            _logger = logger;
            _conf = conf;
            _env = env;
            _dbCon = dbCon;
        }

        public async Task<IActionResult> Index()
        {
            HttpContext _hc = this.HttpContext;
            Exception? exc = null;

            mApp _m_App = new mApp(_hc, this.Request, this.RouteData, _dbCon, _conf, _env, _logger);

            var _e_cmcap = new ent_cmcap(_conf, _dbCon);
            var _e_kraken = new ent_kraken(_conf, _dbCon, _logger);

            //var apiDetailsEncrypted = _e_kraken.doApiDetailsEncrypt();

            var p_kps = new cKrakenPercentageSwingParms
            {
                kapsMinSwing = 0.010M,
                kapsPeriodValue = 5,
                kapsPeriodUnit = "minute",
                kapsPeriodOffset = 0
            };

            var clkps = _e_kraken.doKrakenPercentageSwingList(p_kps);

            string json_cmc = await _e_cmcap.doAPIQuery();

            var krakenData = _e_kraken.doApi_TickerListAsync();

            _m_App._krakenData = krakenData.Result;

            var all = await CryptoComClient.GetTickersAsync(_conf);

            _m_App._cryptocomData = all;

            /*
            var respCrypto = await CryptoComClient.Request(
                methodName: "public/get-ticker",
                @params: new Dictionary<string, object>(),
                conf: _conf
            );

            var json = await respCrypto.Content.ReadAsStringAsync();
            {
                try
                {
                    _m_App._cryptocomData = System.Text.Json.JsonSerializer.Deserialize<CryptoComTickerEnvelope>(json);
                }
                catch (Exception ex)
                {
                    exc = ex;
                }
            }
            */

            return View(_m_App);
        }



    


        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
