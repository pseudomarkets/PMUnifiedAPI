using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using PMUnifiedAPI.Models;

/*
 * Pseudo Markets Unified Web API
 * Quotes API (ETFs and Equities)
 * Author: Shravan Jambukesan <shravan@shravanj.com>
 * (c) 2019 - 2020 Pseudo Markets
 */

namespace PMUnifiedAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class QuotesController : ControllerBase
    {
        private readonly PseudoMarketsDbContext _context;
        private string iexApiKey = "";
        private string avApiKey = "";

        public QuotesController(PseudoMarketsDbContext context)
        {
            _context = context;
            iexApiKey = _context.ApiKeys.Where(x => x.ProviderName == "IEX").Select(x => x.ApiKey).FirstOrDefault();
            avApiKey = _context.ApiKeys.Where(x => x.ProviderName == "AV").Select(x => x.ApiKey).FirstOrDefault();
        }

        // GET: api/Quotes
        [HttpGet]
        public ActionResult LandingPage()
        {
            return Ok("Pseudo Markets Quotes API" + "\n" + "(c) 2019 - 2020 Pseudo Markets");
        }

        // GET: api/Quotes/LatestPrice/MSFT
        [Route("LatestPrice/{symbol}")]
        [HttpGet]
        public async Task<ActionResult> GetLatestPrice(string symbol)
        {
            var client = new HttpClient();
            string endpoint =
                "https://cloud.iexapis.com/stable/tops?token=" + iexApiKey + "&symbols=" + symbol;
            var response = await client.GetAsync(endpoint);
            string jsonResponse = await response.Content.ReadAsStringAsync();
            var topsList = JsonConvert.DeserializeObject<List<IexCloudTops>>(jsonResponse);
            LatestPriceOutput output = new LatestPriceOutput();
            if (topsList.Count > 0)
            {
                var topsData = topsList[0];
                output.symbol = topsData.symbol;
                output.price = topsData.bidPrice;
                output.timestamp = DateTime.Now;
                output.source = "IEX TOPS";
            }
            else
            {
                endpoint = "https://www.alphavantage.co/query?function=GLOBAL_QUOTE&symbol=" + symbol + "&apikey=" +
                           avApiKey;
                var avResponse = await client.GetAsync(endpoint);
                string avJsonResponse = await avResponse.Content.ReadAsStringAsync();
                var avQuote = JsonConvert.DeserializeObject<AlphaVantageGlobalQuote>(avJsonResponse);
                output.symbol = avQuote?.GlobalQuote?.symbol;
                output.price = Convert.ToDouble(avQuote?.GlobalQuote?.price);
                output.timestamp = DateTime.Now;
                output.source = "Alpha Vantage Global Quote";
            }

            Response.ContentType = "application/json";
            return Ok(output);
        }

        public partial class AlphaVantageGlobalQuote
        {
            [JsonProperty("Global Quote")]
            public GlobalQuote GlobalQuote { get; set; }
        }

        public class GlobalQuote
        {
            [JsonProperty("01. symbol")]
            public string symbol { get; set; }

            [JsonProperty("02. open")]
            public string open { get; set; }

            [JsonProperty("03. high")]
            public string high { get; set; }

            [JsonProperty("04. low")]
            public string low { get; set; }

            [JsonProperty("05. price")]
            public string price { get; set; }

            [JsonProperty("06. volume")]
            public string volume { get; set; }

            [JsonProperty("07. latest trading day")]
            public DateTimeOffset latestTradingDay { get; set; }

            [JsonProperty("08. previous close")]
            public string prevClose { get; set; }

            [JsonProperty("09. change")]
            public string change { get; set; }

            [JsonProperty("10. change percent")]
            public string changePercent { get; set; }
        }

        public class LatestPriceOutput
        {
            public string symbol { get; set; }
            public double price { get; set; }
            public DateTime timestamp { get; set; }
            public string source { get; set; }
        }

        public class IexCloudTops
        {
            public string symbol { get; set; }
            public double marketPercent { get; set; }
            public int bidSize { get; set; }
            public double bidPrice { get; set; }
            public int askSize { get; set; }
            public double askPrice { get; set; }
            public int volume { get; set; }
            public double lastSalePrice { get; set; }
            public int lastSaleSize { get; set; }
            public object lastSaleTime { get; set; }
            public long lastUpdated { get; set; }
            public string sector { get; set; }
            public string securityType { get; set; }
        }



    }
}