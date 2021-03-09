using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using PMUnifiedAPI.Models;

namespace PMUnifiedAPI.Standard.Client.Implementation
{
    public static class UnifiedAuthExtensions
    {
        public static HttpClient AddAuthenticationHeaderTo(this Tokens token, HttpClient httpClient)
        {
            httpClient.DefaultRequestHeaders.Add("UnifiedAuth", token.Token);
            return httpClient;
        }
    }
}
