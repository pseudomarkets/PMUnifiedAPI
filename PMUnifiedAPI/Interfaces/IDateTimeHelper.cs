using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/*
 * Pseudo Markets Unified Web API
 * DateTime utilities for determining if the U.S stock market is open
 * Author: Shravan Jambukesan <shravan@shravanj.com>
 * (c) 2019 - 2020 Pseudo Markets
 */

namespace PMUnifiedAPI.Interfaces
{
    public interface IDateTimeHelper
    {
        public bool IsMarketOpen();

        public bool IsMarketHoliday();
    }
}
