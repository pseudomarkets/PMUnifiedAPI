using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using PMUnifiedAPI.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using PMCommonApiModels.RequestModels;
using PMCommonApiModels.ResponseModels;
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
        private string twelveDataApiKey = string.Empty;

        public AccountController(PseudoMarketsDbContext context, IOptions<PseudoMarketsConfig> appConfig)
        {
            _context = context;
            config = appConfig;
            baseUrl = config.Value.AppBaseUrl;
            twelveDataApiKey = _context.ApiKeys.Where(x => x.ProviderName == "TwelveData").Select(x => x.ApiKey)
                .FirstOrDefault();
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
                        var account = await _context.Tokens.Where(x => x.Token == input.Token).Join(_context.Accounts,
                            tokens => tokens.UserID, accounts => accounts.UserID, (tokens, accounts) => accounts).FirstOrDefaultAsync();

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
                        var account = await _context.Tokens.Where(x => x.Token == input.Token).Join(_context.Accounts,
                            tokens => tokens.UserID, accounts => accounts.UserID, (tokens, accounts) => accounts).FirstOrDefaultAsync();

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
                        var account = await _context.Tokens.Where(x => x.Token == input.Token).Join(_context.Accounts,
                                tokens => tokens.UserID, accounts => accounts.UserID, (tokens, accounts) => accounts)
                            .FirstOrDefaultAsync();

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
                        var account = await _context.Tokens.Where(x => x.Token == input.Token).Join(_context.Accounts,
                                tokens => tokens.UserID, accounts => accounts.UserID, (tokens, accounts) => accounts)
                            .FirstOrDefaultAsync();

                        var positions = await _context.Positions.Where(x => x.AccountId == account.Id).ToListAsync();
                        double totalInvestedValue = 0;
                        int numPositions = 0;
                        double totalCurrentValue = 0;
                        double investmentGainOrLoss = 0;
                        double investmentGainOrLossPercentage = 0;

                        TwelveDataClient twelveDataClient = new TwelveDataClient(twelveDataApiKey);

                        foreach (Positions p in positions)
                        {
                            totalInvestedValue += p.Value;
                            string symbol = p.Symbol;
                            var latestPrice = await twelveDataClient.GetRealTimePriceAsync(symbol);
                            totalCurrentValue += latestPrice.Price * p.Quantity;
                            numPositions++;
                        }

                        investmentGainOrLoss = totalCurrentValue - totalInvestedValue;

                        if (investmentGainOrLoss > 0)
                        {
                            investmentGainOrLossPercentage = (investmentGainOrLoss / totalInvestedValue) * 100;
                        }
                        else
                        {
                            investmentGainOrLossPercentage = (-1 * (investmentGainOrLoss / totalInvestedValue)) * 100;
                        }

                        AccountSummaryOutput output = new AccountSummaryOutput()
                        {
                            AccountId = account.Id,
                            AccountBalance = account.Balance,
                            TotalCurrentValue = totalCurrentValue,
                            TotalInvestedValue = totalInvestedValue,
                            NumberOfPositions = numPositions,
                            InvestmentGainOrLoss = investmentGainOrLoss,
                            InvestmentGainOrLossPercentage = investmentGainOrLossPercentage
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
                Log.Fatal(e, $"{nameof(ViewSummary)}");
                return StatusCode(500);
            }
        }
    }
}