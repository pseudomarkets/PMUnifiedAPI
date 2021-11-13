using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PMUnifiedAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using PMConsolidatedTradingPlatform.Client.Core.Implementation;
using PMMarketDataService.DataProvider.Client.Implementation;
using PMUnifiedAPI.AuthenticationService;
using PMUnifiedAPI.Helpers;
using PMUnifiedAPI.Interfaces;
using PMUnifiedAPI.Swagger;

namespace PMUnifiedAPI
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public static IConfiguration Configuration { get; set; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Add Relational Data Store context
            services.AddDbContext<PseudoMarketsDbContext>(options => options.UseSqlServer(Configuration.GetConnectionString("PMDB")));

            // Setup configuration from appsettings.json
            services.Configure<PseudoMarketsConfig>(Configuration.GetSection("PMConfig"));

            // Inject DateTimeHelper for market open check
            services.AddScoped<DateTimeHelper>();

            // Inject Unified Auth Service for shared authentication mechanism 
            services.AddScoped<UnifiedAuthService>();

            // Inject Market Data Service Provider
            services.AddSingleton<MarketDataServiceClient>(new MarketDataServiceClient(new HttpClient(),
                Configuration.GetValue<string>("PMConfig:InternalServiceUsername"),
                Configuration.GetValue<string>("PMConfig:InternalServicePassword"),
                Configuration.GetValue<string>("PMConfig:MarketDataServiceUrl")));

            // Inject Trading Platform Client
            services.AddSingleton<TradingPlatformClient>(
                new TradingPlatformClient(Configuration.GetValue<string>("PMConfig:NetMQServer")));

            // Add all other services
            services.AddHttpClient();
            services.AddControllers();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Version = "v1",
                    Title = "Pseudo Markets Unified API",
                    Description = "Unified API for trading, quotes, account management, and portfolio performance",
                    Contact = new OpenApiContact
                    {
                        Name = "Shravan Jambukesan",
                        Email = "shravan@shravanj.com",
                        Url = new Uri("https://github.com/ShravanJ")
                    },
                    License = new OpenApiLicense
                    {
                        Name = "MIT License",
                        Url = new Uri("https://github.com/pseudomarkets/PMUnifiedAPI/blob/master/LICENSE.txt")
                    }
                });

                c.OperationFilter<RequiredHeaderParameter>();
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            // Add header forwarding when running behind a reverse proxy on Linux hosts
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                app.UseForwardedHeaders(new ForwardedHeadersOptions
                {
                    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
                });
            }
            
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseSwagger();

            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Pseudo Markets Unified API");
            });

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
