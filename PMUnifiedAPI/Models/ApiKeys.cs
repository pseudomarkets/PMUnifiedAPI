using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/*
 * Pseudo Markets Unified Web API
 * ApiKeys Table Model for Entity Framework
 * Author: Shravan Jambukesan <shravan@shravanj.com>
 * (c) 2019 - 2020 Pseudo Markets
 */

namespace PMUnifiedAPI.Models
{
    public class ApiKeys
    {
        public int Id { get; set; }
        public string ProviderName { get; set; }
        public string ApiKey { get; set; }
    }
}
