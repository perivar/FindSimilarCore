using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using FindSimilarServices.Audio;
using FindSimilarServices.Fingerprinting;
using SoundFingerprinting;
using FindSimilarServices;
using FindSimilarServices.Fingerprinting.SQLiteDb;
using FindSimilarServices.Fingerprinting.SQLiteDBService;
using Serilog;
using Microsoft.Extensions.Logging;

namespace FindSimilarClient
{
    public class Startup
    {
        public Startup(IConfiguration configuration, IHostingEnvironment environment)
        {
            Configuration = configuration;
            Environment = environment;
        }

        public IConfiguration Configuration { get; }

        public IHostingEnvironment Environment { get; }


        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => false;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);

            services.AddNodeServices(options =>
            {
                if (Environment.IsDevelopment())
                {
                    options.LaunchWithDebugging = true;
                    options.DebuggingPort = 9229;
                }
            });

            services.AddHttpContextAccessor();

            // add the entity framework core database context 
            var connection = $"Data Source={Configuration["FingerprintDatabase"]}";
            services.AddDbContextPool<SQLiteDbContext>(options =>
            {
                var provider = services.BuildServiceProvider();
                var loggerFactory = provider.GetService<ILoggerFactory>();

                options.UseSerilog(loggerFactory, throwOnQueryWarnings: !Environment.IsProduction());

                // use SQLite
                // options.UseSqlite(connection); // default added as Scoped
            });

            // use SQLite
            // add both the interfaces to FindSimilarSQLiteService
            // services.AddScoped<IModelService, FindSimilarSQLiteService>();
            // services.AddScoped<IFindSimilarDatabase>(x => x.GetService<IModelService>() as IFindSimilarDatabase);

            // use LiteDb
            // add both the interfaces to FindSimilarLiteDBService
            services.AddScoped<IModelService>(x => new FindSimilarLiteDBService(connection));
            services.AddScoped<IFindSimilarDatabase>(x => x.GetService<IModelService>() as IFindSimilarDatabase);

            services.AddScoped<ISoundFingerprinter, SoundFingerprinter>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app)
        {
            if (Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseRequestResponseLogging();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseCookiePolicy();

            app.UseCors(builder => builder
                        .AllowAnyOrigin()
                        .AllowCredentials()
                        .AllowAnyHeader()
                        .AllowAnyMethod());

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
