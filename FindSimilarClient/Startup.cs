using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using FindSimilarServices.Audio;
using FindSimilarServices.Fingerprinting;
using SoundFingerprinting;
using FindSimilarServices;
using FindSimilarServices.Fingerprinting.SQLiteDb;
using Serilog;
using Microsoft.Extensions.Logging;

namespace FindSimilarClient
{
    public class Startup
    {
        public Startup(IConfiguration configuration, IWebHostEnvironment environment)
        {
            Configuration = configuration;
            Env = environment;
        }

        public IWebHostEnvironment Env { get; set; }
        public IConfiguration Configuration { get; }


        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => false;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            var mvcBuilder = services.AddControllersWithViews();
            if (Env.IsDevelopment())
            {
                mvcBuilder.AddRazorRuntimeCompilation();
            }

            services.AddNodeServices(options =>
            {
                if (Env.IsDevelopment())
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

                options.UseSerilog(loggerFactory, throwOnQueryWarnings: !Env.IsProduction());

                // use SQLite
                // options.UseSqlite(connection); // default added as Scoped
            });

            // use SQLite
            // add both the interfaces to FindSimilarSQLiteService
            // services.AddScoped<IModelService, FindSimilarSQLiteService>();
            // services.AddScoped<IFindSimilarDatabase>(x => x.GetService<IModelService>() as IFindSimilarDatabase);

            // use LiteDb
            // add both the interfaces to FindSimilarLiteDBService
            services.AddSingleton<IModelService>(x => new FindSimilarLiteDBService(connection));
            services.AddSingleton<IFindSimilarDatabase>(x => x.GetService<IModelService>() as IFindSimilarDatabase);

            services.AddScoped<ISoundFingerprinter, SoundFingerprinter>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
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
            app.UseRouting();

            app.UseCookiePolicy();

            // global cors policy
            app.UseCors(x => x
                .AllowAnyMethod()
                .AllowAnyHeader()
                .SetIsOriginAllowed(origin => true) // allow any origin
                .AllowCredentials()); // allow credentials

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
