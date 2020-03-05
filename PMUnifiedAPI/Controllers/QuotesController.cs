using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
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
        private string twelveDataApiKey = "";

        public QuotesController(PseudoMarketsDbContext context)
        {
            _context = context;
            iexApiKey = _context.ApiKeys.Where(x => x.ProviderName == "IEX").Select(x => x.ApiKey).FirstOrDefault();
            avApiKey = _context.ApiKeys.Where(x => x.ProviderName == "AV").Select(x => x.ApiKey).FirstOrDefault();
            twelveDataApiKey = _context.ApiKeys.Where(x => x.ProviderName == "TwelveData").Select(x => x.ApiKey)
                .FirstOrDefault();
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
                if (topsData.bidPrice > 0)
                {
                    output.symbol = topsData.symbol;
                    output.price = topsData.bidPrice;
                    output.timestamp = DateTime.Now;
                    output.source = "IEX TOPS";
                }
                else
                {
                    string tdEndpoint = "https://api.twelvedata.com/time_series?symbol=" + symbol + "&interval=1min&apikey=" + twelveDataApiKey;
                    var tdResponse = await client.GetAsync(tdEndpoint);
                    string tdJsonResponse = await tdResponse.Content.ReadAsStringAsync();
                    var tdTimeSeries = JsonConvert.DeserializeObject<TwelveDataTimeSeries>(tdJsonResponse);
                    output.symbol = tdTimeSeries?.Meta?.Symbol;
                    output.price = Convert.ToDouble(tdTimeSeries?.Values[0]?.Close);
                    output.timestamp = DateTime.Now;
                    output.source = "Twelve Data Time Series";
                }
            }
            else
            {
                string tdEndpoint = "https://api.twelvedata.com/time_series?symbol=" + symbol + "&interval=1min&apikey=" + twelveDataApiKey;
                var tdResponse = await client.GetAsync(tdEndpoint);
                string tdJsonResponse = await tdResponse.Content.ReadAsStringAsync();
                var tdTimeSeries = JsonConvert.DeserializeObject<TwelveDataTimeSeries>(tdJsonResponse);
                output.symbol = tdTimeSeries?.Meta?.Symbol;
                output.price = Convert.ToDouble(tdTimeSeries?.Values[0]?.Close);
                output.timestamp = DateTime.Now;
                output.source = "Twelve Data Time Series";
            }

            Response.ContentType = "application/json";
            return Ok(output);
        }

        // GET: /api/Quotes/SmartQuote/AAPL
        [Route("SmartQuote/{symbol}")]
        [HttpGet]
        public async Task<ActionResult> GetSmartPrice(string symbol)
        {
            Controllers.LatestPriceOutput output = new Controllers.LatestPriceOutput();
            var client = new HttpClient();
            string iexEndpoint =
                "https://cloud.iexapis.com/stable/tops?token=" + iexApiKey + "&symbols=" + symbol;
            var response = await client.GetAsync(iexEndpoint);
            string jsonResponse = await response.Content.ReadAsStringAsync();
            var topsList = JsonConvert.DeserializeObject<List<IexCloudTops>>(jsonResponse);
            IexCloudTops topsData;
            double iexTopsPrice = 0;
            double avGloablQuote = 0;
            double twelveDataTimeSeriesClose = 0;
            if (topsList.Count > 0)
            {
                topsData = topsList[0];
                if (topsData.bidPrice > 0)
                {
                    iexTopsPrice = topsData.bidPrice;
                }
            }

            string avEndpoint = "https://www.alphavantage.co/query?function=GLOBAL_QUOTE&symbol=" + symbol + "&apikey=" +
                       avApiKey;
            var avResponse = await client.GetAsync(avEndpoint);
            string avJsonResponse = await avResponse.Content.ReadAsStringAsync();
            var avQuote = JsonConvert.DeserializeObject<AlphaVantageGlobalQuote>(avJsonResponse);
            avGloablQuote = Convert.ToDouble(avQuote?.GlobalQuote?.price);

            string tdEndpoint = "https://api.twelvedata.com/time_series?symbol=" + symbol + "&interval=1min&apikey=" +twelveDataApiKey;
            var tdResponse = await client.GetAsync(tdEndpoint);
            string tdJsonResponse = await tdResponse.Content.ReadAsStringAsync();
            var tdTimeSeries = JsonConvert.DeserializeObject<TwelveDataTimeSeries>(tdJsonResponse);
            twelveDataTimeSeriesClose = Convert.ToDouble(tdTimeSeries?.Values[0]?.Close);

            var prices = new List<double>();
            if (iexTopsPrice > 0)
            {
                prices.Add(iexTopsPrice);
            }

            if (avGloablQuote > 0)
            {
                prices.Add(avGloablQuote);
            }

            if (twelveDataTimeSeriesClose > 0)
            {
                prices.Add(twelveDataTimeSeriesClose);
            }

            double bestPrice = prices.Min();

            if (bestPrice.Equals(iexTopsPrice))
            {
                output.source = "IEX TOPS";
            }

            if (bestPrice.Equals(avGloablQuote))
            {
                output.source = "Alpha Vantage Global Quote";
            }

            if (bestPrice.Equals(twelveDataTimeSeriesClose))
            {
                output.source = "Twelve Data Time Series";
            }

            output.symbol = symbol.ToUpper();
            output.price = bestPrice;
            output.timestamp = DateTime.Now;

            Response.ContentType = "application/json";

            return Ok(output);
        }

        
        [Route("Indices")]
        [HttpGet]
        public async Task<ActionResult> GetIndices()
        {
            var client = new HttpClient();
            string tdEndpoint = "https://api.twelvedata.com/time_series?symbol=SPX,IXIC,DOW&interval=1min&apikey=" + twelveDataApiKey;
            var tdResponse = await client.GetAsync(tdEndpoint);
            string tdJsonResponse = await tdResponse.Content.ReadAsStringAsync();
            var tdIndices = JsonConvert.DeserializeObject<TwelveDataIndices>(tdJsonResponse);
            IndicesOutput output = new IndicesOutput();
            List<StockIndex> indexList = new List<StockIndex>()
            {
                new StockIndex()
                {
                    name = "DOW",
                    price = Convert.ToDouble(tdIndices?.Dow?.Values[0]?.Close)
                },
                new StockIndex()
                {
                    name = "S&P 500",
                    price = Convert.ToDouble(tdIndices?.Spx?.Values[0]?.Close)
                },
                new StockIndex()
                {
                    name = "NASDAQ Composite",
                    price = Convert.ToDouble(tdIndices?.Ixic?.Values[0]?.Close)
                }
            };
            output.indices = indexList;
            output.source = "Twelve Data Time Series";
            output.timestamp = DateTime.Now;

            var outputJson = JsonConvert.SerializeObject(output);

            return Ok(outputJson);
        }

        public class IndicesOutput
        {
            public List<StockIndex> indices;
            public string source { get; set; }
            public DateTime timestamp { get; set; }
        }

        public class StockIndex
        {
            public string name { get; set; }
            public double price { get; set; }
        }

        public partial class TwelveDataIndices
        {
            [JsonProperty("SPX")]
            public TwelveDataTimeSeries Spx { get; set; }

            [JsonProperty("IXIC")]
            public TwelveDataTimeSeries Ixic { get; set; }
            
            [JsonProperty("DOW")]
            public TwelveDataTimeSeries Dow { get; set; }
        }
        

        public partial class TwelveDataTimeSeries
        {
            [JsonProperty("meta")]
            public Meta Meta { get; set; }

            [JsonProperty("values")]
            public Value[] Values { get; set; }

            [JsonProperty("status")]
            public string Status { get; set; }
        }

        public partial class Meta
        {
            [JsonProperty("symbol")]
            public string Symbol { get; set; }

            [JsonProperty("interval")]
            public string Interval { get; set; }

            [JsonProperty("currency")]
            public string Currency { get; set; }

            [JsonProperty("exchange_timezone")]
            public string ExchangeTimezone { get; set; }

            [JsonProperty("exchange")]
            public string Exchange { get; set; }

            [JsonProperty("type")]
            public string Type { get; set; }
        }

        public partial class Value
        {
            [JsonProperty("datetime")]
            public DateTimeOffset Datetime { get; set; }

            [JsonProperty("open")]
            public string Open { get; set; }

            [JsonProperty("high")]
            public string High { get; set; }

            [JsonProperty("low")]
            public string Low { get; set; }

            [JsonProperty("close")]
            public string Close { get; set; }

            [JsonProperty("volume")]
            public long Volume { get; set; }
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