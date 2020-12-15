using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Update;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using PMCommonApiModels.RequestModels;
using PMCommonApiModels.ResponseModels;
using PMCommonEntities.Models;
using PMUnifiedAPI.Models;
using PMDataSynchronizer;
using PMUnifiedAPI.Helpers;
using Serilog;
using PseudoMarketsDbContext = PMUnifiedAPI.Models.PseudoMarketsDbContext;

/*
 * Pseudo Markets Unified Web API
 * Trading API
 * Author: Shravan Jambukesan <shravan@shravanj.com>
 * (c) 2019 - 2020 Pseudo Markets
 */

namespace PMUnifiedAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TradeController : ControllerBase
    {
        private readonly PseudoMarketsDbContext _context;
        private string baseUrl = "";
        private readonly IOptions<PseudoMarketsConfig> config;
        private string SyncDbConnectionString = "";
        private bool DataSyncEnabled = false;
        private DateTimeHelper _dateTimeHelper;

        public TradeController(PseudoMarketsDbContext context, IOptions<PseudoMarketsConfig> appConfig, DateTimeHelper dateTimeHelper)
        {
            _context = context;
            config = appConfig;
            baseUrl = config.Value.AppBaseUrl;
            SyncDbConnectionString = config.Value.DataSyncTargetDb;
            DataSyncEnabled = config.Value.DataSyncEnabled;
            _dateTimeHelper = dateTimeHelper;
        }

        // POST: /api/Trade/Execute
        [Route("Execute")]
        [HttpPost]
        public async Task<ActionResult> ExecuteTrade([FromBody] TradeExecInput input)
        {
            var tokenStatus = TokenHelper.ValidateToken(input.Token);
            switch (tokenStatus)
            {
                case TokenHelper.TokenStatus.Valid:
                {
                    try
                    {
                        var transactionId = Guid.NewGuid().ToString();
                        var userId = await _context.Tokens.Where(x => x.Token == input.Token).Select(x => x.UserID).FirstOrDefaultAsync();
                        var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == userId);
                        var account = await _context.Accounts.FirstOrDefaultAsync(x => x.UserID == user.Id);

                        var client = new HttpClient();
                        var response = await client.GetAsync(baseUrl + "/api/Quotes/SmartQuote/" + input.Symbol);
                        string jsonResponse = await response.Content.ReadAsStringAsync();
                        var jsonObj = JsonConvert.DeserializeObject<LatestPriceOutput>(jsonResponse);
                        double price = jsonObj.price;
                        double value = price * input.Quantity;
                        bool hasSufficientBalance = account.Balance >= value;

                        TradeExecOutput output = new TradeExecOutput();

                        DataSyncManager dataSyncManager = new DataSyncManager(SyncDbConnectionString);

                        if (hasSufficientBalance)
                        {
                            if (input.Type.ToUpper() == "BUY" || input.Type.ToUpper() == "SELL" || input.Type.ToUpper() == "SELLSHORT")
                            {
                                if (price > 0 && input.Quantity > 0)
                                {
                                    if (_dateTimeHelper.IsMarketOpen() && !_dateTimeHelper.IsMarketHoliday())
                                    {
                                            Orders order = new Orders()
                                            {
                                                Symbol = input.Symbol,
                                                Type = input.Type,
                                                Price = price,
                                                Quantity = input.Quantity,
                                                Date = DateTime.Now,
                                                TransactionID = transactionId
                                            };

                                            Transactions transaction = new Transactions()
                                            {
                                                AccountId = account.Id,
                                                TransactionId = transactionId
                                            };

                                            _context.Orders.Add(order);
                                            _context.Transactions.Add(transaction);
                                            await _context.SaveChangesAsync();

                                            if (DataSyncEnabled)
                                            {
                                                Orders replicatedOrder = new Orders()
                                                {
                                                    Symbol = order.Symbol,
                                                    Type = order.Type,
                                                    Price = order.Price,
                                                    Quantity = order.Quantity,
                                                    TransactionID = order.TransactionID,
                                                    Date = DateTime.Now
                                                };

                                                Transactions replicatedTransaction = new Transactions()
                                                {
                                                    TransactionId = order.TransactionID
                                                };

                                                await dataSyncManager.SyncOrders(replicatedOrder, user, DataSyncManager.DbSyncMethod.Insert);
                                                await dataSyncManager.SyncTransactions(replicatedTransaction, user, DataSyncManager.DbSyncMethod.Insert);
                                            }

                                            var createdOrder = await _context.Orders.FirstOrDefaultAsync(x => x.TransactionID == transactionId);

                                            if (input.Type.ToUpper() == "BUY")
                                            {
                                                var doesAccountHaveExistingPosition =
                                                    _context.Positions.Any(x => x.AccountId == account.Id && x.Symbol == input.Symbol);
                                                if (doesAccountHaveExistingPosition == true)
                                                {
                                                    var existingPosition = await _context.Positions
                                                        .Where(x => x.AccountId == account.Id && x.Symbol == input.Symbol).FirstOrDefaultAsync();
                                                    // Long position
                                                    if (existingPosition.Quantity > 0)
                                                    {
                                                        existingPosition.Value += value;
                                                        existingPosition.Quantity += input.Quantity;
                                                        _context.Entry(existingPosition).State = EntityState.Modified;
                                                        account.Balance = account.Balance - value;
                                                        _context.Entry(account).State = EntityState.Modified;
                                                        await _context.SaveChangesAsync();

                                                        if (DataSyncEnabled)
                                                        {

                                                            await dataSyncManager.SyncPositions(existingPosition, user,
                                                                DataSyncManager.DbSyncMethod.Update);
                                                            await dataSyncManager.SyncAccounts(account, user, DataSyncManager.DbSyncMethod.Update);

                                                        }

                                                    }
                                                    else // Short position
                                                    {
                                                        if (Math.Abs(existingPosition.Quantity) == input.Quantity)
                                                        {
                                                            double gainOrLoss = existingPosition.Value - value;
                                                            account.Balance += gainOrLoss;
                                                            _context.Entry(account).State = EntityState.Modified;
                                                            _context.Entry(existingPosition).State = EntityState.Deleted;
                                                            await _context.SaveChangesAsync();

                                                            if (DataSyncEnabled)
                                                            {
                                                                await dataSyncManager.SyncAccounts(account, user,
                                                                    DataSyncManager.DbSyncMethod.Update);
                                                                await dataSyncManager.SyncPositions(existingPosition, user,
                                                                    DataSyncManager.DbSyncMethod.Delete);
                                                            }
                                                        }
                                                        else
                                                        {
                                                            existingPosition.Value = existingPosition.Value - value;
                                                            existingPosition.Quantity += input.Quantity;
                                                            account.Balance += existingPosition.Value - value;
                                                            _context.Entry(existingPosition).State = EntityState.Modified;
                                                            _context.Entry(account).State = EntityState.Modified;
                                                            await _context.SaveChangesAsync();

                                                            if (DataSyncEnabled)
                                                            {

                                                                await dataSyncManager.SyncAccounts(account, user,
                                                                    DataSyncManager.DbSyncMethod.Update);
                                                                await dataSyncManager.SyncPositions(existingPosition, user,
                                                                    DataSyncManager.DbSyncMethod.Update);

                                                            }
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    Positions position = new Positions()
                                                    {
                                                        AccountId = account.Id,
                                                        OrderId = createdOrder.Id,
                                                        Value = value,
                                                        Symbol = input.Symbol,
                                                        Quantity = input.Quantity
                                                    };
                                                    _context.Positions.Add(position);
                                                    await _context.SaveChangesAsync();

                                                    account.Balance = account.Balance - value;
                                                    _context.Entry(account).State = EntityState.Modified;
                                                    await _context.SaveChangesAsync();

                                                    if (DataSyncEnabled)
                                                    {
                                                        Positions replicatedPosition = new Positions()
                                                        {
                                                            OrderId = createdOrder.Id,
                                                            Value = value,
                                                            Symbol = input.Symbol,
                                                            Quantity = input.Quantity
                                                        };

                                                        await dataSyncManager.SyncPositions(replicatedPosition, user,
                                                            DataSyncManager.DbSyncMethod.Insert);
                                                        await dataSyncManager.SyncAccounts(account, user, DataSyncManager.DbSyncMethod.Update);
                                                    }
                                                }
                                            }
                                            else if (input.Type.ToUpper() == "SELL")
                                            {
                                                var doesAccountHaveExistingPosition =
                                                    _context.Positions.Any(x => x.AccountId == account.Id && x.Symbol == input.Symbol);
                                                if (doesAccountHaveExistingPosition == true)
                                                {
                                                    var existingPosition = await _context.Positions
                                                        .Where(x => x.AccountId == account.Id && x.Symbol == input.Symbol).FirstOrDefaultAsync();
                                                    if (input.Quantity == existingPosition.Quantity)
                                                    {
                                                        account.Balance = account.Balance + value;
                                                        _context.Entry(existingPosition).State = EntityState.Deleted;
                                                        _context.Entry(account).State = EntityState.Modified;
                                                        await _context.SaveChangesAsync();

                                                        if (DataSyncEnabled)
                                                        {
                                                            await dataSyncManager.SyncAccounts(account, user,
                                                                DataSyncManager.DbSyncMethod.Update);
                                                            await dataSyncManager.SyncPositions(existingPosition, user,
                                                                DataSyncManager.DbSyncMethod.Delete);
                                                        }
                                                    }
                                                    else
                                                    {
                                                        existingPosition.Value -= value;
                                                        existingPosition.Quantity -= input.Quantity;
                                                        _context.Entry(existingPosition).State = EntityState.Modified;
                                                        account.Balance = account.Balance + value;
                                                        _context.Entry(account).State = EntityState.Modified;
                                                        await _context.SaveChangesAsync();

                                                        if (DataSyncEnabled)
                                                        {
                                                            await dataSyncManager.SyncAccounts(account, user,
                                                                DataSyncManager.DbSyncMethod.Update);
                                                            await dataSyncManager.SyncPositions(existingPosition, user,
                                                                DataSyncManager.DbSyncMethod.Update);
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    output.Status = StatusMessages.InvalidPositionsMessage + input.Symbol;
                                                    return Ok(output);
                                                }
                                            }
                                            else if (input.Type.ToUpper() == "SELLSHORT")
                                            {
                                                var doesAccountHaveExistingPosition =
                                                    _context.Positions.Any(x => x.AccountId == account.Id && x.Symbol == input.Symbol);
                                                if (doesAccountHaveExistingPosition == true)
                                                {
                                                    var existingPosition = await _context.Positions
                                                        .Where(x => x.AccountId == account.Id && x.Symbol == input.Symbol)
                                                        .FirstOrDefaultAsync();
                                                    if (existingPosition.Quantity > 0)
                                                    {
                                                        _context.Entry(createdOrder).State = EntityState.Deleted;
                                                        await _context.SaveChangesAsync();
                                                        if (DataSyncEnabled)
                                                        {
                                                            await dataSyncManager.SyncOrders(createdOrder, user, DataSyncManager.DbSyncMethod.Delete);
                                                        }

                                                        output.Status =
                                                            StatusMessages.InvalidShortPositionMessage;
                                                        return Ok(output);
                                                    }
                                                    else
                                                    {
                                                        existingPosition.Value += value;
                                                        existingPosition.Quantity += input.Quantity * -1;
                                                        _context.Entry(existingPosition).State = EntityState.Modified;
                                                        await _context.SaveChangesAsync();

                                                        if (DataSyncEnabled)
                                                        {
                                                            await dataSyncManager.SyncAccounts(account, user,
                                                                DataSyncManager.DbSyncMethod.Update);
                                                            await dataSyncManager.SyncPositions(existingPosition, user,
                                                                DataSyncManager.DbSyncMethod.Update);
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    Positions position = new Positions()
                                                    {
                                                        AccountId = account.Id,
                                                        OrderId = createdOrder.Id,
                                                        Value = value,
                                                        Symbol = input.Symbol,
                                                        Quantity = input.Quantity * -1
                                                    };

                                                    _context.Positions.Add(position);
                                                    account.Balance = account.Balance - value;
                                                    _context.Entry(account).State = EntityState.Modified;
                                                    await _context.SaveChangesAsync();

                                                    if (DataSyncEnabled)
                                                    {
                                                        await dataSyncManager.SyncPositions(position, user,
                                                            DataSyncManager.DbSyncMethod.Insert);
                                                        await dataSyncManager.SyncAccounts(account, user, DataSyncManager.DbSyncMethod.Update);
                                                    }
                                                }
                                            }

                                            output.Status = StatusMessages.SuccessMessage;
                                            output.Order = createdOrder;
                                            return Ok(output);
                                    }
                                    else
                                    {
                                        CreateQueuedOrder(input, userId);
                                        output.Status =
                                            "Market is closed, order has been queued to be filled on next market open";
                                        return Ok(output);
                                    }
                                }
                                else
                                {
                                    output.Status = StatusMessages.InvalidSymbolOrQuantityMessage;
                                    return Ok(output);
                                }
                            }
                            else
                            {
                                output.Status = StatusMessages.InvalidOrderTypeMessage;
                                return Ok(output);
                            }
                        }
                        else
                        {
                            output.Status = StatusMessages.InsufficientBalanceMessage;
                            return Ok(output);
                        }

                    }
                    catch (Exception e)
                    {
                        StatusOutput status = new StatusOutput()
                        {
                            message = "An internal error occured, please try again later."
                        };
                        Log.Fatal(e, $"{nameof(ExecuteTrade)}");
                        return Ok(status);
                    }
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

        private void CreateQueuedOrder(TradeExecInput input, int userId)
        {
            try
            {
                QueuedOrders order = new QueuedOrders()
                {
                    OrderDate = DateTime.Today,
                    OrderType = input.Type,
                    Quantity = input.Quantity,
                    Symbol = input.Symbol,
                    UserId = userId,
                    IsOpenOrder = true
                };

                _context.QueuedOrders.Add(order);

                _context.SaveChanges();
            }
            catch (Exception e)
            {
                Log.Fatal(e, $"{nameof(CreateQueuedOrder)}");
            }
        }

    }
}