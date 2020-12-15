using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Aerospike.Client;
using PMUnifiedAPI.Interfaces;
using PMUnifiedAPI.Models;
using Log = Serilog.Log;

/*
 * Pseudo Markets Unified Web API
 * DateTime utilities for determining if the U.S stock market is open
 * Author: Shravan Jambukesan <shravan@shravanj.com>
 * (c) 2019 - 2020 Pseudo Markets
 */

namespace PMUnifiedAPI.Helpers
{
    public class DateTimeHelper : IDateTimeHelper
    {
        private readonly PseudoMarketsDbContext _context;
        public DateTimeHelper(PseudoMarketsDbContext context)
        {
            this._context = context;
        }

        public bool IsMarketOpen()
        {
            // Regular market hours are between 9:30 AM and 4:00 PM EST, Monday through Friday
            if (DateTime.Now.ToUniversalTime() >=
                new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 8, 30, 0).ToUniversalTime() &&
                DateTime.Now.ToUniversalTime() <=
                new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 15, 0, 0).ToUniversalTime() &&
                (DateTime.Now.DayOfWeek != DayOfWeek.Saturday || DateTime.Now.DayOfWeek != DayOfWeek.Sunday))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool IsMarketHoliday()
        {
            try
            {
                var isHoliday = _context.MarketHolidays.Any(x => x.HolidayDate == DateTime.Today);
                return isHoliday;
            }
            catch (Exception e)
            {
                Log.Fatal(e, $"{nameof(IsMarketHoliday)}");
                return true;
            }
            
        }
    }
}
