using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using PMCommonApiModels.RequestModels;
using PMCommonApiModels.ResponseModels;
using PMUnifiedAPI.Models;
using PMUnifiedAPI.Standard.Client.Interfaces;

namespace PMUnifiedAPI.Standard.Client.Implementation
{
    public class PseudoMarketsClient : IPseudoMarketsClient
    {
        private readonly HttpClient _httpClient;
        public PseudoMarketsClient(string baseUrl)
        {
            _httpClient = new HttpClient()
            {
                BaseAddress = new Uri(baseUrl)
            };
        }

        public LatestPriceOutput GetLatestPrice(string symbol)
        {
            throw new NotImplementedException();
        }

        public LatestPriceOutput GetSmartPrice(string symbol)
        {
            throw new NotImplementedException();
        }

        public DetailedQuoteOutput GetDetailedQuoteOutput(string symbol, string interval = "1min")
        {
            throw new NotImplementedException();
        }

        public Tokens Login(string username, string password)
        {
            return default;
        }
    }
}
