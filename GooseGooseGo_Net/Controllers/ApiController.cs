

using GooseGooseGo_Net.ef;
using GooseGooseGo_Net.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GooseGooseGo_Net.Controllers
{
    [Route("api")]
    [ApiController]
    public class ApiController : ControllerBase
    {
        private readonly ILogger<mApp> _logger;
        private dbContext _dbCon;
        private IConfiguration _conf;
        private IWebHostEnvironment _env;
        private IHttpClientFactory _httpClientFactory;

        private readonly ent_mexc _svc;

        public ApiController(ent_mexc svc)
        {
            _svc = svc;
        }

        public ApiController(ILogger<mApp> logger, IConfiguration conf, IWebHostEnvironment env, IHttpClientFactory httpClientFactory, dbContext dbCon)
        {
            _logger = logger;
            _dbCon = dbCon;
            _conf = conf;
            _env = env;
            _httpClientFactory = httpClientFactory;
        }

        [HttpPost("doAssetPercentageSwingList")]
        public ApiResponse<List<cAssetPercentageSwing>?> doAssetPercentageSwingList(cAssetPercentageSwingParms p)
        {
            var ret = new ent_asset(_conf, _logger, _httpClientFactory).doAssetPercentageSwingList(_dbCon, p);
            return ret;
        }

        [HttpPost("doAssetInfoList")]
        public ApiResponse<List<cAssetInfo>?> doAssetInfoList()
        {
            var ret = new ent_asset(_conf, _logger, _httpClientFactory).doAssetInfoList(_dbCon);
            return ret;
        }

        [HttpPost("doKrakenReturnPortfolio")]
        public async Task<ActionResult<ApiResponse<List<cKrakenPortfolio>>>> DoKrakenReturnPortfolio(CancellationToken ct)
        {
            var svc = new ent_kraken(_conf, _logger, _httpClientFactory, _dbCon);

            var ret = await svc.doKrakenReturnPortfolio(_dbCon, ct);

            if (ret is null)
                return StatusCode(502, "Upstream returned null.");

            return Ok(ret);
        }

        [HttpPost("doMexcReturnPortfolio")]
        public async Task<ActionResult<ApiResponse<List<cMexcOrderLotSummaryGroup<string, cMexcOrderLotSummary>>>>> DoMexcReturnPortfolio(CancellationToken ct)
        {
            //var svc = new ent_mexc(_conf, _logger, _httpClientFactory, _dbCon);

            var ret = await _svc.doMexcReturnPortfolio(_dbCon, ct);

            if (ret is null)
                return StatusCode(502, "Upstream returned null.");

            return Ok(ret);
        }

        // POST api/mexc/sell100
        [HttpPost("doMexcSell100")]
        public async Task<ActionResult<object>> doMexcSell100(cMexcSellParms p, CancellationToken ct)
        {
            //var _svc = new ent_mexc(_conf, _logger, _httpClientFactory, _dbCon);
            // 1) pre-check
            var preview = await _svc.doMexcPreviewSell100Async(_dbCon, p, ct);

            if (!string.IsNullOrEmpty(preview.ReasonIfBlocked))
                return BadRequest(new { ok = false, preview });

            if (p.mxsellDryRun) return Ok(new { ok = true, preview });

            // 2) execute
            var result = await _svc.doMexcExecuteSell100Async(_dbCon, p, ct);
            return Ok(new { ok = true, preview, result });
        }
    }

}


