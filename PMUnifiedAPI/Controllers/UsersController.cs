﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PMCommonApiModels.RequestModels;
using PMCommonApiModels.ResponseModels;
using PMCommonEntities.Models;
using PMDataSynchronizer;
using PMUnifiedAPI.Helpers;
using PMUnifiedAPI.Models;
using Serilog;
using PseudoMarketsDbContext = PMUnifiedAPI.Models.PseudoMarketsDbContext;

/*
 * Pseudo Markets Unified Web API
 * Users API
 * Author: Shravan Jambukesan <shravan@shravanj.com>
 * (c) 2019 - 2020 Pseudo Markets
 */

namespace PMUnifiedAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly PseudoMarketsDbContext _context;
        private readonly bool _dataSyncEnabled = false;
        private readonly string _syncDbConnectionString;

        public UsersController(PseudoMarketsDbContext context, IOptions<PseudoMarketsConfig> appConfig)
        {
            _context = context;
            var config = appConfig;
            _dataSyncEnabled = config.Value.DataSyncEnabled;
            _syncDbConnectionString = config.Value.DataSyncTargetDb;
        }

        // POST: api/Users/Register
        [Route("Register")]
        [HttpPost]
        public async Task<ActionResult<Users>> RegisterUser(LoginInput input)
        {
            try
            {
                if (!_context.Users.Any(x => x.Username == input.username))
                {
                    DataSyncManager dataSyncManager = new DataSyncManager(_syncDbConnectionString);

                    byte[] salt = new byte[128 / 8];
                    using (var rng = RandomNumberGenerator.Create())
                    {
                        rng.GetBytes(salt);
                    }

                    Users newUser = new Users()
                    {
                        Username = input.username,
                        Password = PasswordHashHelper.GetHash(input.password, salt),
                        Salt = salt,
                        EnvironmentId = RDSEnums.EnvironmentId.ProductionPrimary
                    };

                    _context.Users.Add(newUser);
                    await _context.SaveChangesAsync();

                    Users createdUser = await _context.Users.FirstOrDefaultAsync(x => x.Username == input.username);
                    Tokens newToken = new Tokens()
                    {
                        UserID = createdUser.Id,
                        Token = TokenHelper.GenerateToken(input.username, TokenHelper.TokenType.Standard),
                        EnvironmentId = RDSEnums.EnvironmentId.ProductionPrimary
                    };

                    Accounts newAccount = new Accounts()
                    {
                        UserID = createdUser.Id,
                        Balance = 1000000.99,
                        EnvironmentId = RDSEnums.EnvironmentId.ProductionPrimary
                    };

                    _context.Tokens.Add(newToken);
                    _context.Accounts.Add(newAccount);
                    await _context.SaveChangesAsync();

                    if (_dataSyncEnabled)
                    {
                        Users replicatedUser = new Users()
                        {
                            Username = newUser.Username,
                            Password = newUser.Password,
                            Salt = newUser.Salt,
                            EnvironmentId = RDSEnums.EnvironmentId.ProductionSecondary
                        };

                        await dataSyncManager.SyncNewUser(replicatedUser, newToken.Token);

                    }

                    StatusOutput output = new StatusOutput()
                    {
                        message = StatusMessages.UserCreatedMessage
                    };
                    return Ok(output);
                }
                else
                {
                    StatusOutput output = new StatusOutput()
                    {
                        message = StatusMessages.UserExistsMessage
                    };
                    return Ok(output);
                }
            }
            catch (Exception e)
            {
                Log.Fatal(e, $"{nameof(RegisterUser)}");
                return StatusCode(500);
            }
        }

        // POST: api/Users/Login
        [Route("Login")]
        [HttpPost]
        public async Task<ActionResult<Tokens>> LoginUser(LoginInput loginInput)
        {
            try
            {
                var exists = _context.Users.Any(x => x.Username == loginInput.username);
                if (exists)
                {
                    var user = await _context.Users.Where(x => x.Username == loginInput.username).FirstOrDefaultAsync();

                    if (PasswordHashHelper.GetHash(loginInput.password, user.Salt) == user.Password)
                    {
                        var token = _context.Tokens.FirstOrDefault(x => x.UserID == user.Id);

                        // Create a new token on every successful login

                        if (token == null)
                            return default;

                        token.Token = TokenHelper.GenerateToken(user.Username, TokenHelper.TokenType.Standard);
                        _context.Entry(token).State = EntityState.Modified;

                        await _context.SaveChangesAsync();
                        return Ok(token);
                    }
                    else
                    {
                        return Unauthorized();
                    }
                }
                else
                {
                    return Unauthorized();
                }
            }
            catch (Exception e)
            {
                Log.Fatal(e, $"{nameof(LoginUser)}");
                return StatusCode(500);
            }
        }

        // POST: api/Users/ChangePassword
        [Route("ChangePassword")]
        [HttpPost]
        public async Task<ActionResult> ChangePassword(ChangePasswordInput input)
        {
            try
            {
                byte[] salt = new byte[128 / 8];
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(salt);
                }

                var exists = _context.Users.Any(x => x.Username == input.username);
                if (exists)
                {
                    DataSyncManager dataSyncManager = new DataSyncManager(_syncDbConnectionString);
                    var user = await _context.Users.Where(x => x.Username == input.username).FirstOrDefaultAsync();

                    if (PasswordHashHelper.GetHash(input.old_password, user.Salt) == user.Password)
                    {
                        var token = _context.Tokens.FirstOrDefault(x => x.UserID == user.Id);
                        user.Password = PasswordHashHelper.GetHash(input.new_password, salt);
                        user.Salt = salt;
                        _context.Entry(user).State = EntityState.Modified;

                        token.Token = TokenHelper.GenerateToken(input.username, TokenHelper.TokenType.Standard);
                        _context.Entry(token).State = EntityState.Modified;

                        await _context.SaveChangesAsync();

                        if (_dataSyncEnabled)
                        {
                            await dataSyncManager.SyncUsers(user, DataSyncManager.DbSyncMethod.Update);
                            await dataSyncManager.SyncTokens(token, user, DataSyncManager.DbSyncMethod.Update);
                        }

                        StatusOutput output = new StatusOutput()
                        {
                            message = "Password changed"
                        };

                        return Ok(output);
                    }
                    else
                    {
                        return Unauthorized();
                    }
                }
                else
                {
                    return Unauthorized();
                }
            }
            catch (Exception e)
            {
                Log.Fatal(e, $"{nameof(ChangePassword)}");
                return StatusCode(500);
            }
        }

        private bool UsersExists(int id)
        {
            return _context.Users.Any(e => e.Id == id);
        }
    }
}
