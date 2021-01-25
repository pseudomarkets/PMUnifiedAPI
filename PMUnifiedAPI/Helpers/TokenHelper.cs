using JWT;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JWT.Algorithms;
using JWT.Builder;
using JWT.Exceptions;
using JWT.Serializers;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

/*
 * Pseudo Markets Unified Web API
 * JWT Token Generator and Validator
 * Author: Shravan Jambukesan <shravan@shravanj.com>
 * (c) 2019 - 2020 Pseudo Markets
 */

namespace PMUnifiedAPI.Helpers
{
    public class TokenHelper
    {
        private static readonly string Secret = Startup.Configuration.GetValue<string>("PMConfig:TokenSecretKey");
        private static readonly string Issuer = Startup.Configuration.GetValue<string>("PMConfig:TokenIssuer");
        private static readonly string Source = Startup.Configuration.GetValue<string>("PMConfig:ServerId");

        public static TokenStatus ValidateToken(string token)
        {
            var status = TokenStatus.Unknown;
            try
            {
                IJsonSerializer serializer = new JsonNetSerializer();
                IDateTimeProvider provider = new UtcDateTimeProvider();
                IJwtValidator validator = new JwtValidator(serializer, provider);
                IBase64UrlEncoder urlEncoder = new JwtBase64UrlEncoder();
                IJwtDecoder decoder = new JwtDecoder(serializer, validator, urlEncoder, new HMACSHA256Algorithm());

                var jsonString = decoder.Decode(token, Secret, verify: true);

                var json = JsonConvert.DeserializeObject<TokenJson>(jsonString);

                if (json != null)
                {
                    // Standard tokens are valid for 1 hour, special tokens have no expiration
                    if (json.iss == Issuer && json.src == Source && (DateTime.Now <= json.ts.AddHours(1) || json.typ == TokenType.Special))
                    {
                        status = TokenStatus.Valid;
                    }
                    else
                    {
                        status = TokenStatus.Expired;
                    }
                }
            }
            catch (TokenExpiredException)
            {
                status = TokenStatus.Expired;
            }
            catch (SignatureVerificationException)
            {
                status = TokenStatus.Unknown;
            }

            return status;
        }

        public static string GenerateToken(string username, TokenType type)
        {
            var token = new JwtBuilder()
                .WithAlgorithm(new HMACSHA256Algorithm())
                .WithSecret(Secret)
                .AddClaim("sub", username)
                .AddClaim("typ", type)
                .AddClaim("iss", Issuer)
                .AddClaim("src", Source)
                .AddClaim("ts", DateTime.Now)
                .Encode();
            return token;
        }

        public class TokenJson
        {
            public string sub { get; set; }
            public TokenType typ { get; set; }
            public string iss { get; set; }
            public string src { get; set; }
            public DateTime ts { get; set; }
        }

        public enum TokenStatus
        {
            Valid = 0,
            Expired = 1,
            Unknown = 2
        }

        public enum TokenType
        {
            Standard = 1,
            Special = 2
        }
    }
}
