using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PMCommonEntities.Models;

/*
 * Pseudo Markets Unified Web API
 * Database Context Model for Entity Framework
 * Author: Shravan Jambukesan <shravan@shravanj.com>
 * (c) 2019 - 2020 Pseudo Markets
 */

namespace PMUnifiedAPI.Models
{
    public class PseudoMarketsDbContext : DbContext
    {
        public PseudoMarketsDbContext(DbContextOptions<PseudoMarketsDbContext> options) : base(options)
        {

        }

        public DbSet<Users> Users { get; set; }
        public DbSet<Tokens> Tokens { get; set; }
        public DbSet<ApiKeys> ApiKeys { get; set; }
        public DbSet<Accounts> Accounts { get; set; }
        public DbSet<Positions> Positions { get; set; }
        public DbSet<Orders> Orders { get; set; }
        public DbSet<Transactions> Transactions { get; set; }
        public DbSet<QueuedOrders> QueuedOrders { get; set; }
        public DbSet<MarketHolidays> MarketHolidays { get; set; }
    }
}
