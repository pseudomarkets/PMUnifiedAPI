using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Permissions;
using System.Threading.Tasks;

/*
 * Pseudo Markets Unified Web API
 * Positions Table Model for Entity Framework
 * Author: Shravan Jambukesan <shravan@shravanj.com>
 * (c) 2019 - 2020 Pseudo Markets
 */

namespace PMUnifiedAPI.Models
{
    public class Positions
    {
        public int Id { get; set; }
        public int AccountId { get; set; }
        public int OrderId { get; set; }
        public double Value { get; set; }
        public string Symbol { get; set; }
        public int Quantity { get; set; }
    }
}
