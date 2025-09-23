

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
        private readonly ILogger<ApiController> _logger;
        private dbContext _dbCon;
        private IConfiguration _conf;
        private IWebHostEnvironment _env;

        public ApiController(ILogger<ApiController> logger, IConfiguration conf, IWebHostEnvironment env, dbContext dbCon)
        {
            _logger = logger;
            _dbCon = dbCon;
            _conf = conf;
            _env = env;
        }

        [HttpPost("doKrakenPercentageSwingList")]
        public ApiResponse<List<cKrakenPercentageSwing>?> doKrakenPercentageSwingList(cKrakenPercentageSwingParms p)
        {
            var ret = new ent_kraken(_conf, _dbCon, _logger).doKrakenPercentageSwingList(p);
            return ret;
        }

    }

}


