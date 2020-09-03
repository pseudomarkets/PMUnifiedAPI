﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using PMUnifiedAPI.Models;

namespace PMUnifiedAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AboutController : ControllerBase
    {
        private readonly IOptions<PseudoMarketsConfig> config;

        public AboutController(IOptions<PseudoMarketsConfig> appConfig)
        {
            config = appConfig;
        }

        // GET: /api/About
        [HttpGet]
        public ActionResult About()
        {
            return Ok("Pseudo Markets Unified API" + "\n" + "Version: " + config.Value.AppVersion + "\n" + "Server: " + config.Value.ServerId + "\n" + "Environment: " + config.Value.Environment + "\n" + "Data Sync Enabled: " + config.Value.DataSyncEnabled + "\n" + "(c) 2019 - 2020 Pseudo Markets");
        }

        [Route("AboutJson")]
        [HttpGet]
        public ActionResult AboutJson()
        {
            AboutJsonResponse jsonResponse = new AboutJsonResponse()
            {
                AppName = "Pseudo Markets Unified API",
                AppVersion = config.Value.AppVersion,
                ServerId = config.Value.ServerId,
                Environment = config.Value.Environment,
                DataSyncEnabled = config.Value.DataSyncEnabled,
                Copyright = "(c) 2019 - 2020 Pseudo Markets"
            };

            var response = JsonConvert.SerializeObject(jsonResponse);

            return Ok(response);
        }
    }

    public class AboutJsonResponse
    {
        public string AppName { get; set; }
        public string AppVersion { get; set; }
        public string ServerId { get; set; }
        public string Environment { get; set; }
        public bool DataSyncEnabled { get; set; }
        public string Copyright { get; set; }
    }
}