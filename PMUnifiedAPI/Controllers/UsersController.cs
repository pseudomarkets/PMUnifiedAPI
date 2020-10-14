using System;
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
using PMDataSynchronizer;
using PMUnifiedAPI.Helpers;
using PMUnifiedAPI.Models;
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
        private readonly IOptions<PseudoMarketsConfig> config;
        private bool DataSyncEnabled = false;
        private string SyncDbConnectionString = "";

        public UsersController(PseudoMarketsDbContext context, IOptions<PseudoMarketsConfig> appConfig)
        {
            _context = context;
            config = appConfig;
            DataSyncEnabled = config.Value.DataSyncEnabled;
            SyncDbConnectionString = config.Value.DataSyncTargetDb;
        }

        // POST: api/Users/Register
        [Route("Register")]
        [HttpPost]
        public async Task<ActionResult<Users>> RegisterUser(LoginInput input)
        {
            if (! _context.Users.Any(x => x.Username == input.username))
            {
                DataSyncManager dataSyncManager = new DataSyncManager(SyncDbConnectionString);

                byte[] salt = new byte[128 / 8];
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(salt);
                }

                Users newUser = new Users()
                {
                    Username = input.username,
                    Password = PasswordHashHelper.GetHash(input.password, salt),
                    Salt = salt
                };

                _context.Users.Add(newUser);
                await _context.SaveChangesAsync();

                Users createdUser = await _context.Users.FirstOrDefaultAsync(x => x.Username == input.username);
                Tokens newToken = new Tokens()
                {
                    UserID = createdUser.Id,
                    Token = TokenHelper.GenerateToken(input.username)
                };
                Accounts newAccount = new Accounts()
                {
                    UserID = createdUser.Id,
                    Balance = 1000000.99
                };
                _context.Tokens.Add(newToken);
                _context.Accounts.Add(newAccount);
                await _context.SaveChangesAsync();

                if (DataSyncEnabled)
                {
                    Users replicatedUser = new Users()
                    {
                        Username = newUser.Username,
                        Password = newUser.Password,
                        Salt = newUser.Salt
                    };

                    await dataSyncManager.SyncNewUser(replicatedUser, newToken.Token);

                }

                StatusOutput output = new StatusOutput()
                {
                    message = "User created"
                };
                return Ok(output);
            }
            else
            {
                StatusOutput output = new StatusOutput()
                {
                    message = "User already exists"
                };
                return Ok(output);
            }
        }

        // POST: api/Users/Login
        [Route("Login")]
        [HttpPost]
        public async Task<ActionResult<Tokens>> LoginUser(LoginInput loginInput)
        {
            var exists = _context.Users.Any(x => x.Username == loginInput.username);
            if (exists)
            {
                var user = await _context.Users.Where(x => x.Username == loginInput.username).FirstOrDefaultAsync();

                if (PasswordHashHelper.GetHash(loginInput.password, user.Salt) == user.Password)
                {
                    var token = _context.Tokens.FirstOrDefault(x => x.UserID == user.Id);
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

        // POST: api/Users/ChangePassword
        [Route("ChangePassword")]
        [HttpPost]
        public async Task<ActionResult> ChangePassword(ChangePasswordInput input)
        {
            byte[] salt = new byte[128 / 8];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            var exists = _context.Users.Any(x => x.Username == input.username);
            if (exists)
            {
                DataSyncManager dataSyncManager = new DataSyncManager(SyncDbConnectionString);
                var user = await _context.Users.Where(x => x.Username == input.username).FirstOrDefaultAsync();

                if (PasswordHashHelper.GetHash(input.old_password, user.Salt) == user.Password)
                {
                    var token = _context.Tokens.FirstOrDefault(x => x.UserID == user.Id);
                    user.Password = PasswordHashHelper.GetHash(input.new_password, salt);
                    user.Salt = salt;
                    _context.Entry(user).State = EntityState.Modified;

                    token.Token = TokenHelper.GenerateToken(input.username);
                    _context.Entry(token).State = EntityState.Modified;

                    await _context.SaveChangesAsync();

                    if (DataSyncEnabled)
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

        private bool UsersExists(int id)
        {
            return _context.Users.Any(e => e.Id == id);
        }
    }
}
