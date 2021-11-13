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
using PMMarketDataService.DataProvider.Client.Implementation;
using PMUnifiedAPI.Models;
using Serilog;
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
        private readonly MarketDataServiceClient _marketDataClient;

        public QuotesController(MarketDataServiceClient marketDataClient)
        {
            _marketDataClient = marketDataClient;
        }

        // GET: api/Quotes
        [HttpGet]
        public ActionResult LandingPage()
        {
            return Ok($"Pseudo Markets Quotes API \n(c) 2019 - {DateTime.Now.Year} Pseudo Markets");
        }

        // GET: api/Quotes/LatestPrice/MSFT
        [Route("LatestPrice/{symbol}")]
        [HttpGet]
        public async Task<ActionResult> GetLatestPrice(string symbol)
        {
            try
            {
                var price = await _marketDataClient.GetLatestPrice(symbol);
                Response.ContentType = "application/json";
                return Ok(price);
            }
            catch (Exception e)
            {
                Log.Fatal(e, $"{nameof(GetLatestPrice)}");
                return StatusCode(500);
            }
        }

        // GET: /api/Quotes/SmartQuote/AAPL
        [Route("SmartQuote/{symbol}")]
        [HttpGet]
        public async Task<ActionResult> GetSmartPrice(string symbol)
        {
            try
            {
                var price = await _marketDataClient.GetAggregatePrice(symbol);
                Response.ContentType = "application/json";
                return Ok(price);
            }
            catch (Exception e)
            {
                Log.Fatal(e, $"{nameof(GetSmartPrice)}");
                return StatusCode(500);
            }
        }

        // GET: /api/Quotes/DetailedQuote/AMZN/1day
        [Route("DetailedQuote/{symbol}/{interval}")]
        [HttpGet]
        public async Task<ActionResult> GetDetailedQuote(string symbol, string interval)
        {
            try
            {
                var detailedQuote = await _marketDataClient.GetDetailedQuote(symbol, interval);
                Response.ContentType = "application/json";
                return Ok(detailedQuote);
            }
            catch (Exception e)
            {
                Log.Fatal(e, $"{nameof(GetDetailedQuote)}");
                return StatusCode(500);
            }
        }

        
        [Route("Indices")]
        [HttpGet]
        public async Task<ActionResult> GetIndices()
        {
            try
            {
                var indices = await _marketDataClient.GetIndices();
                var output = JsonConvert.SerializeObject(indices);
                Response.ContentType = "application/json";
                return Ok(output);
            }
            catch (Exception e)
            {
                Log.Fatal(e, $"{nameof(GetIndices)}");
                return StatusCode(500);
            }
        }
    }
}