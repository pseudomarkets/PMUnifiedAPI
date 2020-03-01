using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/*
 * Pseudo Markets Unified Web API
 * Users Table Model for Entity Framework
 * Author: Shravan Jambukesan <shravan@shravanj.com>
 * (c) 2019 - 2020 Pseudo Markets
 */

namespace PMUnifiedAPI.Models
{
    public class Users
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public byte[] Salt { get; set; }
    }
}
