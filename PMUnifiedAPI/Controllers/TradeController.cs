using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PMCommonApiModels.RequestModels;
using PMCommonApiModels.ResponseModels;
using PMCommonEntities.Models;
using PMCommonEntities.Models.TradingPlatform;
using PMConsolidatedTradingPlatform.Client.Core.Implementation;
using PMDataSynchronizer;
using PMMarketDataService.DataProvider.Client.Implementation;
using PMUnifiedAPI.AuthenticationService;
using PMUnifiedAPI.Helpers;
using PMUnifiedAPI.Models;
using PMUnifiedAPI.TradingPlatform;
using Serilog;
using PseudoMarketsDbContext = PMUnifiedAPI.Models.PseudoMarketsDbContext;

/*
 * Pseudo Markets Unified Web API
 * Trading API
 * Author: Shravan Jambukesan <shravan@shravanj.com>
 * (c) 2019 - 2021 Pseudo Markets
 */

namespace PMUnifiedAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TradeController : ControllerBase
    {
        private readonly PseudoMarketsDbContext _context;
        private readonly string _syncDbConnectionString;
        private readonly bool _dataSyncEnabled;
        private readonly DateTimeHelper _dateTimeHelper;
        private readonly UnifiedAuthService _unifiedAuth;
        private readonly MarketDataServiceClient _marketDataService;
        private readonly bool _consolidatedTradingPlatformEnabled;
        private readonly TradingPlatformClient _tradingPlatformClient;

        public TradeController(PseudoMarketsDbContext context, IOptions<PseudoMarketsConfig> appConfig,
            DateTimeHelper dateTimeHelper, UnifiedAuthService unifiedAuth, MarketDataServiceClient marketDataService,
            TradingPlatformClient tradingPlatformClient)
        {
            _context = context;
            var config = appConfig;
            _syncDbConnectionString = config.Value.DataSyncTargetDb;
            _dataSyncEnabled = config.Value.DataSyncEnabled;
            _dateTimeHelper = dateTimeHelper;
            _unifiedAuth = unifiedAuth;
            _marketDataService = marketDataService;
            _consolidatedTradingPlatformEnabled = config.Value.ConsolidatedTradingPlatformEnabled;
            _tradingPlatformClient = tradingPlatformClient;
        }

        // POST: /api/Trade/Execute
        [Route("Execute")]
        [HttpPost]
        public async Task<ActionResult> ExecuteTrade([FromBody] TradeExecInput input)
        {
            var authResult = await _unifiedAuth.AuthenticateUser(Request.HttpContext);

            var tokenStatus = authResult.TokenStatus;

            switch (tokenStatus)
            {
                case TokenHelper.TokenStatus.Valid:
                {
                    TradeExecOutput response = default;

                    // Use the Consolidated Trading Platform Service (Real-time and/or RDS)
                    if (_consolidatedTradingPlatformEnabled)
                    {
                        if (authResult.Account != null)
                        {
                            var consolidatedTradeRequest = new ConsolidatedTradeRequest()
                            {
                                AccountId = authResult.Account.Id,
                                OrderAction = input.Type,
                                OrderOrigin = ConsolidatedTradeEnums.ConsolidatedOrderOrigin.PseudoMarkets,
                                OrderTiming = ConsolidatedTradeEnums.ConsolidatedOrderTiming.DayOnly,
                                OrderType = ConsolidatedTradeEnums.ConsolidatedOrderType.Market,
                                Quantity = input.Quantity,
                                Symbol = input.Symbol,
                                EnforceMarketOpenCheck = true
                            };

                            _tradingPlatformClient.SendTradeRequest(consolidatedTradeRequest);

                            var ctpResponse = _tradingPlatformClient.GetTradeResponse();

                            if (ctpResponse != null)
                            {
                                response = new TradeExecOutput()
                                {
                                    Order = ctpResponse?.Order,
                                    StatusCode = GetTradeStatusCodeUsing(ctpResponse.StatusCode),
                                    StatusMessage = ctpResponse?.StatusMessage
                                };
                            }
                        }
                    }
                    else
                    {
                        // Fallback to legacy trading code using Relational Data Store (RDS)
                        response = await RdsFallback.ProcessTradingRequestUsingRdsFallback(authResult.User,
                            authResult.Account, input, _context, _marketDataService, _dateTimeHelper);
                    }

                    return Ok(response);
                }
                case TokenHelper.TokenStatus.Expired:
                {
                    TradeExecOutput status = new TradeExecOutput
                    {
                        Order = null,
                        StatusCode = TradeStatusCodes.ExecutionError,
                        StatusMessage = StatusMessages.ExpiredTokenMessage
                    };

                    return Ok(status);
                }
                default:
                {
                    TradeExecOutput status = new TradeExecOutput
                    {
                        Order = null,
                        StatusCode = TradeStatusCodes.ExecutionError,
                        StatusMessage = StatusMessages.InvalidTokenMessage
                    };

                    return Ok(status);
                }
            }
        }

        private TradeStatusCodes GetTradeStatusCodeUsing(ConsolidatedTradeEnums.TradeStatusCodes ctpStatusCode)
        {
            switch (ctpStatusCode)
            {
                case ConsolidatedTradeEnums.TradeStatusCodes.ExecutionOk:
                    return TradeStatusCodes.ExecutionOk;
                case ConsolidatedTradeEnums.TradeStatusCodes.ExecutionQueued:
                    return TradeStatusCodes.ExecutionQueued;
                case ConsolidatedTradeEnums.TradeStatusCodes.ExecutionError:
                    return TradeStatusCodes.ExecutionError;
                default:
                    throw new ArgumentOutOfRangeException(nameof(ctpStatusCode), ctpStatusCode, null);
            }
        }
    }
}