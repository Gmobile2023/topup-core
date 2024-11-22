using Autofac;
using Autofac.Extensions.DependencyInjection;
using HLS.Paygate.TopupGw.Components;
using HLS.Paygate.TopupGw.Domains.BusinessServices;
using Infrastructure.AppVersion;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServiceStack;

namespace HLS.Paygate.TopupGw.Hosting;

public class Startup : ModularStartup
{
    public Startup(IConfiguration configuration) : base(configuration)
    {
        Configuration = configuration;
    }

    public ILifetimeScope AutofacContainer { get; private set; }

    public new void ConfigureServices(IServiceCollection services)
    {
        // services.AddAutofac();
        services.AddOptions();
        // services.AddMvcCore(options => options.EnableEndpointRouting = false)
        //     .AddAuthorization();

        // services.AddIdentity<ApplicationUser, IdentityRole>(options => {
        //         options.User.AllowedUserNameCharacters = null;
        //     })
        //     // .AddEntityFrameworkStores<ApplicationDbContext>()
        //     .AddDefaultTokenProviders();

        // services.AddAuthentication("Bearer")
        //     // .AddJwtBearer("Bearer", options =>
        //     // {
        //     //     options.Authority = "http://localhost:5000";
        //     //     options.RequireHttpsMetadata = false;
        //     //     options.Audience = "default-api";
        //     //
        //     // })
        //     .AddIdentityServerAuthentication("Bearer", options =>
        //     {
        //         options.Authority = "http://localhost:5000"; // Configuration["AuthServer:Authority"];
        //         options.ApiName = "default-api"; // Configuration["AuthServer:ApiName"];
        //         options.RequireHttpsMetadata = false;
        //     });

        // services.AddCors(options =>
        // {
        //     options.AddPolicy("CorsPolicy",
        //         builder =>
        //         {
        //             builder.WithOrigins(Configuration["CorsOrigins"]
        //                     .Split(",")
        //                     .ToArray())
        //                 .SetIsOriginAllowedToAllowWildcardSubdomains()
        //                 .AllowAnyHeader()
        //                 .AllowAnyMethod()
        //                 .AllowCredentials();
        //         });
        // });
    }

    public void ConfigureContainer(ContainerBuilder builder)
    {
        // Register your own things directly with Autofac here. Don't
        // call builder.Populate(), that happens in AutofacServiceProviderFactory
        // for you.
        builder.RegisterModule(new TopupGwModule());
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, AlarmAppVersion version, BackgroundServices backgroundService )
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
            version.AlarmVersion();
        }
        //backgroundService.StartJob();
        // app.UseAuthorization();
        // app.UseAuthentication();
        AutofacContainer = app.ApplicationServices.GetAutofacRoot();
        // app.UseMvc();
        // loggerFactory.AddSerilog();
        var appHost = new AppHost(Configuration, AutofacContainer)
        {
            AppSettings = new NetCoreAppSettings(Configuration)
        };
        app.UseServiceStack(appHost);
        // loggerFactory.AddSerilog();
        // app.UseCors("CorsPolicy");
    }
}