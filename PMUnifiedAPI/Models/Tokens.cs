using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/*
 * Pseudo Markets Unified Web API
 * Tokens Table Model for Entity Framework
 * Author: Shravan Jambukesan <shravan@shravanj.com>
 * (c) 2019 - 2020 Pseudo Markets
 */

namespace PMUnifiedAPI.Models
{
    public class Tokens
    {
        public int Id { get; set; }
        public int UserID { get; set; }
        public string Token { get; set; }
    }
}
