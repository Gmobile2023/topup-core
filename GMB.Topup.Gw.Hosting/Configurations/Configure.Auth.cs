using System.IdentityModel.Tokens.Jwt;
using GMB.Topup.Gw.Hosting.Configurations;
using GMB.Topup.Shared;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Logging;
using ServiceStack;
using ServiceStack.Auth;

[assembly: HostingStartup(typeof(ConfigureAuth))]

namespace GMB.Topup.Gw.Hosting.Configurations;

public class ConfigureAuth : IHostingStartup
{
    public void Configure(IWebHostBuilder builder)
    {
        builder
            .ConfigureServices((context, services) =>
            {
                JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                }).AddJwtBearer(options =>
                {
                    if (context.Configuration == null) return;
                    options.Authority = context.Configuration["OAuth:IdentityServer:AuthorizeUrl"];
                    options.RequireHttpsMetadata = false;
                    options.Audience = context.Configuration["OAuth:IdentityServer:Audience"];
                });
            })
            .ConfigureAppHost(appHost =>
            {
                var appSettings = appHost.AppSettings;
                appHost.Plugins.Add(new AuthFeature(() => new CustomUserSession(),
                    new IAuthProvider[]
                    {
                        //new CredentialsAuthProvider(AppSettings),
                        new NetCoreIdentityAuthProvider(appSettings)
                        {
                            RoleClaimType = "role",
                            PopulateSessionFilter = (session, principal, req) =>
                            {
                                ((CustomUserSession)session).AccountCode =
                                    ((CustomUserSession)session)?.Meta["account_code"];
                                if (session != null)
                                    ((CustomUserSession)session).ClientId =
                                        ((CustomUserSession)session)?.Meta["client_id"];
                            }
                        }
                    })
                );
            });
    }
}