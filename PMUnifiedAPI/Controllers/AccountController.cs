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
        private string baseUrl = "";
        private readonly IOptions<PseudoMarketsConfig> config;
        private string _twelveDataApiKey = string.Empty;
        private readonly string _portfolioPerformanceApiBaseUrl;
        private readonly string _internalServiceAuthUsername;
        private readonly string _internalServiceAuthPassword;

        public AccountController(PseudoMarketsDbContext context, IOptions<PseudoMarketsConfig> appConfig)
        {
            _context = context;
            config = appConfig;
            baseUrl = config.Value.AppBaseUrl;
            _twelveDataApiKey = _context.ApiKeys.Where(x => x.ProviderName == "TwelveData").Select(x => x.ApiKey)
                .FirstOrDefault();
            _portfolioPerformanceApiBaseUrl = config.Value.PerformanceReportingApiUrl;
            _internalServiceAuthUsername = config.Value.InternalServiceUsername;
            _internalServiceAuthPassword = config.Value.InternalServicePassword;
        }

        // POST: /api/Account/Positions
        [HttpPost]
        [Route("Positions")]
        public async Task<ActionResult> ViewPositions(ViewAccount input)
        {
            try
            {
                var tokenStatus = TokenHelper.ValidateToken(input.Token);
                switch (tokenStatus)
                {
                    case TokenHelper.TokenStatus.Valid:
                    {
                        var account = await GetAccountFromToken(input.Token);

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

        // POST: /api/Account/Transactions
        [HttpPost]
        [Route("Transactions")]
        public async Task<ActionResult> ViewTransactions(ViewAccount input)
        {
            try
            {
                var tokenStatus = TokenHelper.ValidateToken(input.Token);
                switch (tokenStatus)
                {
                    case TokenHelper.TokenStatus.Valid:
                    {
                        var account = await GetAccountFromToken(input.Token);

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

        // POST: /api/Balance
        [HttpPost]
        [Route("Balance")]
        public async Task<ActionResult> ViewBalance(ViewAccount input)
        {
            try
            {
                var tokenStatus = TokenHelper.ValidateToken(input.Token);
                switch (tokenStatus)
                {
                    case TokenHelper.TokenStatus.Valid:
                    {
                        var account = await GetAccountFromToken(input.Token);

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

        // POST: /api/Account/PortfolioPerformance
        [HttpPost]
        [Route("PortfolioPerformance/{date}")]
        public async Task<ActionResult> ViewPortfolioPerformance([FromBody] ViewAccount input, string date)
        {
            try
            {
                var tokenStatus = TokenHelper.ValidateToken(input.Token);
                switch (tokenStatus)
                {
                    case TokenHelper.TokenStatus.Valid:
                    {
                        var account = await GetAccountFromToken(input.Token);

                        var accountId = account.Id;

                        HttpClient client = new HttpClient()
                        {
                            BaseAddress = new Uri(_portfolioPerformanceApiBaseUrl)
                        };


                        var byteArray = Encoding.ASCII.GetBytes($"{_internalServiceAuthUsername}:{_internalServiceAuthPassword}");
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

                        var response = await client.GetStringAsync($"PerformanceReport/GetPerformanceReport/{accountId}/{date}");

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

        // POST: /api/Account/Summary
        [HttpPost]
        [Route("Summary")]
        public async Task<ActionResult> ViewSummary(ViewAccount input)
        {
            try
            {
                var tokenStatus = TokenHelper.ValidateToken(input.Token);
                switch (tokenStatus)
                {
                    case TokenHelper.TokenStatus.Valid:
                    {
                        var account = await GetAccountFromToken(input.Token);

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

        private async Task<Accounts> GetAccountFromToken(string token)
        {
            var account = await _context.Tokens.Where(x => x.Token == token).Join(_context.Accounts,
                    tokens => tokens.UserID, accounts => accounts.UserID, (tokens, accounts) => accounts)
                .FirstOrDefaultAsync();

            return account;
        }
    }
}