

using System.Net;
using System.Web;

namespace GooseGooseGo_Net.ef
{

    /*
     * 
     *
      CoinMarket Cap API Handler - https://coinmarketcap.com/
     * 
     * 
     */

    public class ent_cmcap
    {
        private IConfiguration _conf = null!;
        private dbContext _dbCon;

        public ent_cmcap(IConfiguration conf, dbContext dbCon)
        {
            _conf = conf;
            _dbCon = dbCon;
        }

        public async Task<string> doAPIQuery()
        {
            var confSection = _conf.GetSection("CMC_API");
            var API_SANDBOX_URL = confSection["CMC_API_SANDBOX_URL"]!;
            var API_URL = confSection["CMC_API_URL"]!;
            var API_KEY= confSection["CMC_API_KEY"]!;
            var keySecret = confSection["KRAKEN_SECRET"]!;

            var queryString = HttpUtility.ParseQueryString(string.Empty);
            queryString["start"] = "1";
            queryString["limit"] = "5000";
            queryString["convert"] = "USD";

            var URL = new UriBuilder($"{API_URL}/v1/cryptocurrency/map");
            //URL.Query = queryString.ToString();

            var request = new HttpRequestMessage(HttpMethod.Get, URL.ToString());
            request.Headers.Add("X-CMC_PRO_API_KEY", API_KEY);
            request.Headers.Add("Accept", "application/json");

            var client = new HttpClient();
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            string result = await response.Content.ReadAsStringAsync();
            return result;
        }
    }
}
