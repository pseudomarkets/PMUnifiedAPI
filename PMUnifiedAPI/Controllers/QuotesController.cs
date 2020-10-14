using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using PMCommonApiModels.ResponseModels;
using PMUnifiedAPI.Models;
using TwelveDataSharp;

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
        private readonly IOptions<PseudoMarketsConfig> config;

        public QuotesController(PseudoMarketsDbContext context, IOptions<PseudoMarketsConfig> appConfig)
        {
            _context = context;
            config = appConfig;
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
                    TwelveDataClient twelveDataClient = new TwelveDataClient(twelveDataApiKey);
                    var price = await twelveDataClient.GetRealTimePriceAsync(symbol);
                    output.symbol = symbol;
                    output.price = price.Price;
                    output.timestamp = DateTime.Now;
                    output.source = "Twelve Data Real Time Price";
                }
            }
            else
            {
                TwelveDataClient twelveDataClient = new TwelveDataClient(twelveDataApiKey);
                var price = await twelveDataClient.GetRealTimePriceAsync(symbol);
                output.symbol = symbol;
                output.price = price.Price;
                output.timestamp = DateTime.Now;
                output.source = "Twelve Data Real Time Price";
            }

            Response.ContentType = "application/json";
            return Ok(output);
        }

        // GET: /api/Quotes/SmartQuote/AAPL
        [Route("SmartQuote/{symbol}")]
        [HttpGet]
        public async Task<ActionResult> GetSmartPrice(string symbol)
        {
            LatestPriceOutput output = new LatestPriceOutput();
            var client = new HttpClient();
            string iexEndpoint =
                "https://cloud.iexapis.com/stable/tops?token=" + iexApiKey + "&symbols=" + symbol;
            var response = await client.GetAsync(iexEndpoint);
            string jsonResponse = await response.Content.ReadAsStringAsync();
            var topsList = JsonConvert.DeserializeObject<List<IexCloudTops>>(jsonResponse);
            IexCloudTops topsData;
            double iexTopsPrice = 0;
            double avGloablQuote = 0;
            double twelveDataRealTimePrice = 0;
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
            var avQuote = JsonConvert.DeserializeObject<AlphaVantage.AlphaVantageGlobalQuote>(avJsonResponse);
            avGloablQuote = Convert.ToDouble(avQuote?.GlobalQuote?.price);

            TwelveDataClient twelveDataClient = new TwelveDataClient(twelveDataApiKey);
            var price = await twelveDataClient.GetRealTimePriceAsync(symbol);
            twelveDataRealTimePrice = price.Price;

            var prices = new List<double>();
            if (iexTopsPrice > 0)
            {
                prices.Add(iexTopsPrice);
            }

            if (avGloablQuote > 0)
            {
                prices.Add(avGloablQuote);
            }

            if (twelveDataRealTimePrice > 0)
            {
                prices.Add(twelveDataRealTimePrice);
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

            if (bestPrice.Equals(twelveDataRealTimePrice))
            {
                output.source = "Twelve Data Real Time Price";
            }

            output.symbol = symbol.ToUpper();
            output.price = bestPrice;
            output.timestamp = DateTime.Now;

            Response.ContentType = "application/json";

            return Ok(output);
        }

        // GET: /api/Quotes/DetailedQuote/AMZN/1day
        [Route("DetailedQuote/{symbol}/{interval}")]
        [HttpGet]
        public async Task<ActionResult> GetDetailedQuote(string symbol, string interval)
        {
            TwelveDataClient twelveDataClient = new TwelveDataClient(twelveDataApiKey);
            var detailedQuote = await twelveDataClient.GetQuoteAsync(symbol, interval);
            DetailedQuoteOutput output = new DetailedQuoteOutput()
            {
                name = detailedQuote.Name,
                symbol = symbol,
                open = detailedQuote.Open,
                high = detailedQuote.High,
                low = detailedQuote.Low,
                close = detailedQuote.Close,
                volume = detailedQuote.Volume,
                previousClose = detailedQuote.PreviousClose,
                change = detailedQuote.Change,
                changePercentage = detailedQuote.PercentChange,
                timestamp = DateTime.Now
            };

            Response.ContentType = "application/json";
            return Ok(output);
        }

        
        [Route("Indices")]
        [HttpGet]
        public async Task<ActionResult> GetIndices()
        {
            var client = new HttpClient();
            string tdEndpoint = "https://api.twelvedata.com/time_series?symbol=SPX,IXIC,DJI&interval=1min&apikey=" + twelveDataApiKey;
            var tdResponse = await client.GetAsync(tdEndpoint);
            string tdJsonResponse = await tdResponse.Content.ReadAsStringAsync();
            var tdIndices = JsonConvert.DeserializeObject<TwelveData.TwelveDataIndices>(tdJsonResponse);
            IndicesOutput output = new IndicesOutput();
            List<StockIndex> indexList = new List<StockIndex>()
            {
                new StockIndex()
                {
                    name = "DOW",
                    points = Convert.ToDouble(tdIndices?.Dow?.Values[0]?.Close)
                },
                new StockIndex()
                {
                    name = "S&P 500",
                    points = Convert.ToDouble(tdIndices?.Spx?.Values[0]?.Close)
                },
                new StockIndex()
                {
                    name = "NASDAQ Composite",
                    points = Convert.ToDouble(tdIndices?.Ixic?.Values[0]?.Close)
                }
            };
            output.indices = indexList;
            output.source = "Twelve Data Time Series";
            output.timestamp = DateTime.Now;

            var outputJson = JsonConvert.SerializeObject(output);

            return Ok(outputJson);
        }
    }
}