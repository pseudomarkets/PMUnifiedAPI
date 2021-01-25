using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using PMCommonApiModels.ResponseModels;
using PMUnifiedAPI.Helpers;
using PMUnifiedAPI.Models;

/*
 * Pseudo Markets Unified Web API
 * Market Status API
 * Author: Shravan Jambukesan <shravan@shravanj.com>
 * (c) 2019 - 2020 Pseudo Markets
 */

namespace PMUnifiedAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MarketOpenCheckController : ControllerBase
    {
        private readonly DateTimeHelper _dateTimeHelper;
        public MarketOpenCheckController(DateTimeHelper dateTimeHelper)
        {
            _dateTimeHelper = dateTimeHelper;
        }

        // GET: /api/MaketOpenCheck
        [HttpGet]
        public ActionResult Get()
        {
            MarketStatusOutput output = new MarketStatusOutput();
            if (_dateTimeHelper.IsMarketOpen())
            {
                output.MarketStatus = StatusMessages.MarketIsOpenMessage;
            }
            else
            {
                output.MarketStatus = StatusMessages.MarketNotOpenMessage;
            }

            var serializedOutput = JsonConvert.SerializeObject(output);

            return Ok(serializedOutput);
        }
    }
}
