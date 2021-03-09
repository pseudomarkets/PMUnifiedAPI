using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Text;
using PMCommonApiModels.ResponseModels;
using PMUnifiedAPI.Models;
using PMUnifiedAPI.Standard.Client.Implementation;

namespace PMUnifiedAPI.Standard.Client.Interfaces
{
    public interface IPseudoMarketsClient
    {
        LatestPriceOutput GetLatestPrice(string symbol);
        LatestPriceOutput GetSmartPrice(string symbol);
        DetailedQuoteOutput GetDetailedQuoteOutput(string symbol, string interval = "1min");
        Tokens Login(string username, string password);
    }
}
