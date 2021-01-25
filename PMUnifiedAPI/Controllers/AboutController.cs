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

/*
 * Pseudo Markets Unified Web API
 * About API
 * Author: Shravan Jambukesan <shravan@shravanj.com>
 * (c) 2019 - 2020 Pseudo Markets
 */

namespace PMUnifiedAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AboutController : ControllerBase
    {
        private readonly IOptions<PseudoMarketsConfig> _config;

        public AboutController(IOptions<PseudoMarketsConfig> appConfig)
        {
            _config = appConfig;
        }

        // GET: /api/About
        [HttpGet]
        public ActionResult About()
        {
            return Ok("Pseudo Markets Unified API" + "\n" + "Version: " + _config.Value.AppVersion + "\n" + "Server: " + _config.Value.ServerId + "\n" + "Environment: " + _config.Value.Environment + "\n" + "Data Sync Enabled: " + _config.Value.DataSyncEnabled + "\n" + $"(c) 2019 - {DateTime.Now.Year} Pseudo Markets");
        }

        [Route("AboutJson")]
        [HttpGet]
        public ActionResult AboutJson()
        {
            AboutJsonResponse jsonResponse = new AboutJsonResponse()
            {
                AppName = "Pseudo Markets Unified API",
                AppVersion = _config.Value.AppVersion,
                ServerId = _config.Value.ServerId,
                Environment = _config.Value.Environment,
                DataSyncEnabled = _config.Value.DataSyncEnabled,
                Copyright = $"(c) 2019 - {DateTime.Now.Year} Pseudo Markets"
            };

            var response = JsonConvert.SerializeObject(jsonResponse);

            return Ok(response);
        }
    }

}