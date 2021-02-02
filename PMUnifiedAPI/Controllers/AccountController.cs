using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using PMUnifiedAPI.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using PMCommonApiModels.RequestModels;
using PMCommonApiModels.ResponseModels;
using PMCommonEntities.Models.PerformanceReporting;
using PMUnifiedAPI.AuthenticationService;
using PMUnifiedAPI.Helpers;
using Serilog;
using TwelveDataSharp;

/*
 * Pseudo Markets Unified Web API
 * Account API
 * Author: Shravan Jambukesan <shravan@shravanj.com>
 * (c) 2019 - 2020 Pseudo Markets
 */

namespace PMUnifiedAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly PseudoMarketsDbContext _context;
        private readonly string _portfolioPerformanceApiBaseUrl;
        private readonly string _internalServiceAuthUsername;
        private readonly string _internalServiceAuthPassword;
        private readonly UnifiedAuthService _unifiedAuth;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly HttpClient _httpClient;

        public AccountController(PseudoMarketsDbContext context, IOptions<PseudoMarketsConfig> appConfig, UnifiedAuthService authService, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            var config = appConfig;
            _portfolioPerformanceApiBaseUrl = config.Value.PerformanceReportingApiUrl;
            _internalServiceAuthUsername = config.Value.InternalServiceUsername;
            _internalServiceAuthPassword = config.Value.InternalServicePassword;
            _unifiedAuth = authService;
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _httpClient = _httpClientFactory.CreateClient();
            _httpClient.BaseAddress = new Uri(_portfolioPerformanceApiBaseUrl);
        }

        // GET: /api/Account/Positions
        [HttpGet]
        [Route("Positions")]
        public async Task<ActionResult> ViewPositions()
        {
            try
            {
                var authResult = await _unifiedAuth.AuthenticateUser(Request.HttpContext);

                var tokenStatus = authResult.Item3;

                switch (tokenStatus)
                {
                    case TokenHelper.TokenStatus.Valid:
                    {
                        var account = authResult.Item2;

                        var positions = await _context.Positions.Where(x => x.AccountId == account.Id).ToListAsync();
                        return Ok(positions);

                    }
                    case TokenHelper.TokenStatus.Expired:
                    {
                        StatusOutput status = new StatusOutput()
                        {
                            message = StatusMessages.ExpiredTokenMessage
                        };

                        return Ok(status);
                    }
                    default:
                    {
                        StatusOutput status = new StatusOutput()
                        {
                            message = StatusMessages.InvalidTokenMessage
                        };

                        return Ok(status);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Fatal(e, $"{nameof(ViewPositions)}");
                return StatusCode(500);
            }
        }

        // GET: /api/Account/Transactions
        [HttpGet]
        [Route("Transactions")]
        public async Task<ActionResult> ViewTransactions()
        {
            try
            {
                var authResult = await _unifiedAuth.AuthenticateUser(Request.HttpContext);

                var tokenStatus = authResult.Item3;
                
                switch (tokenStatus)
                {
                    case TokenHelper.TokenStatus.Valid:
                    {
                        var account = authResult.Item2;

                        var transactions = await _context.Transactions.Where(x => x.AccountId == account.Id).ToListAsync();

                        var orders = transactions.Join(_context.Orders, transactions1 => transactions1.TransactionId,
                            orders1 => orders1.TransactionID, (transactions1, orders1) => orders1).ToList();

                        return Ok(orders);
                    }
                    case TokenHelper.TokenStatus.Expired:
                    {
                        StatusOutput status = new StatusOutput()
                        {
                            message = StatusMessages.ExpiredTokenMessage
                        };

                        return Ok(status);
                    }
                    default:
                    {
                        StatusOutput status = new StatusOutput()
                        {
                            message = StatusMessages.InvalidTokenMessage
                        };

                        return Ok(status);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Fatal(e, $"{nameof(ViewTransactions)}");
                return StatusCode(500);
            }
        }

        // GET: /api/Balance
        [HttpGet]
        [Route("Balance")]
        public async Task<ActionResult> ViewBalance()
        {
            try
            {
                var authResult = await _unifiedAuth.AuthenticateUser(Request.HttpContext);

                var tokenStatus = authResult.Item3;

                switch (tokenStatus)
                {
                    case TokenHelper.TokenStatus.Valid:
                    {
                        var account = authResult.Item2;

                        AccountBalanceOutput output = new AccountBalanceOutput()
                        {
                            AccountId = account.Id,
                            AccountBalance = account.Balance
                        };

                        return Ok(output);
                    }
                    case TokenHelper.TokenStatus.Expired:
                    {
                        StatusOutput status = new StatusOutput()
                        {
                            message = StatusMessages.ExpiredTokenMessage
                        };

                        return Ok(status);
                    }
                    default:
                    {
                        StatusOutput status = new StatusOutput()
                        {
                            message = StatusMessages.InvalidTokenMessage
                        };

                        return Ok(status);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Fatal(e, $"{nameof(ViewBalance)}");
                return StatusCode(500);
            }
        }

        // GET: /api/Account/PortfolioPerformance
        [HttpGet]
        [Route("PortfolioPerformance/{date}")]
        public async Task<ActionResult> ViewPortfolioPerformance(string date)
        {
            try
            {
                var authResult = await _unifiedAuth.AuthenticateUser(Request.HttpContext);

                var tokenStatus = authResult.Item3;

                switch (tokenStatus)
                {
                    case TokenHelper.TokenStatus.Valid:
                    {
                        var account = authResult.Item2;

                        var accountId = account.Id;

                        var byteArray = Encoding.ASCII.GetBytes($"{_internalServiceAuthUsername}:{_internalServiceAuthPassword}");
                        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

                        var response = await _httpClient.GetStringAsync($"PerformanceReport/GetPerformanceReport/{accountId}/{date}");

                        var responseJson = JsonConvert.DeserializeObject<PortfolioPerformanceReport>(response);

                        return Ok(responseJson);
                    }
                    case TokenHelper.TokenStatus.Expired:
                    {
                        StatusOutput status = new StatusOutput()
                        {
                            message = StatusMessages.ExpiredTokenMessage
                        };

                        return Ok(status);
                    }
                    default:
                    {
                        StatusOutput status = new StatusOutput()
                        {
                            message = StatusMessages.InvalidTokenMessage
                        };

                        return Ok(status);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Fatal(e, $"{nameof(ViewSummary)}");
                return StatusCode(500);
            }
        }

        // GET: /api/Account/Summary
        [HttpGet]
        [Route("Summary")]
        public async Task<ActionResult> ViewSummary()
        {
            try
            {
                var authResult = await _unifiedAuth.AuthenticateUser(Request.HttpContext);

                var tokenStatus = authResult.Item3;

                switch (tokenStatus)
                {
                    case TokenHelper.TokenStatus.Valid:
                    {
                        var account = authResult.Item2;

                        var accountId = account.Id;

                        HttpClient client = new HttpClient()
                        {
                            BaseAddress = new Uri(_portfolioPerformanceApiBaseUrl)
                        };


                        var byteArray = Encoding.ASCII.GetBytes($"{_internalServiceAuthUsername}:{_internalServiceAuthPassword}");
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

                        var response = await client.GetStringAsync($"PerformanceReport/GetCurrentPerformance/{accountId}");

                        var responseJson = JsonConvert.DeserializeObject<PortfolioPerformanceReport>(response);

                        return Ok(responseJson);

                    }
                    case TokenHelper.TokenStatus.Expired:
                    {
                        StatusOutput status = new StatusOutput()
                        {
                            message = StatusMessages.ExpiredTokenMessage
                        };

                        return Ok(status);
                    }
                    default:
                    {
                        StatusOutput status = new StatusOutput()
                        {
                            message = StatusMessages.InvalidTokenMessage
                        };

                        return Ok(status);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Fatal(e, $"{nameof(ViewSummary)}");
                return StatusCode(500);
            }
        }
    }
}