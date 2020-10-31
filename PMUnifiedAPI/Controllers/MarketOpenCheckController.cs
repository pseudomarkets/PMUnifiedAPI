using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using PMCommonApiModels.ResponseModels;
using PMUnifiedAPI.Models;

namespace PMUnifiedAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MarketOpenCheckController : ControllerBase
    {
        // GET: api/MaketOpenCheck
        [HttpGet]
        public ActionResult Get(int id)
        {
            MarketStatusOutput output = new MarketStatusOutput();
            if (DateTime.Now.ToUniversalTime() >= new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 13, 30, 0).ToUniversalTime() &&
                DateTime.Now.ToUniversalTime() <= new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 20, 0, 0).ToUniversalTime())
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
