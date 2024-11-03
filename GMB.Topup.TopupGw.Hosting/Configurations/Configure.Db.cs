using HLS.Paygate.Gw.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ServiceStack;
using ServiceStack.OrmLite;
using ServiceStack.OrmLite.SqlServer;

namespace HLS.Paygate.TopupGw.Hosting.Configurations
{
    public class ConfigureDb : IConfigureServices, IConfigureAppHost
    {
        public ConfigureDb(IConfiguration configuration)
        {
            Configuration = configuration;
        }
    
        private IConfiguration Configuration { get; }
    
        public void Configure(IAppHost appHost)
        {
            var scriptMethodses = appHost.GetPlugin<SharpPagesFeature>()?.ScriptMethods;
            scriptMethodses?.Add(new DbScriptsAsync());

            // using (var db = appHost.Resolve<IPaygateConnectionFactory>().Open())
            // {
            //     db.CreateTableIfNotExists<Domain.Entities.Command>();
            //     db.CreateTableIfNotExists<Domain.Entities.Action>(); 
            // }
            
            //appHost.Resolve<IAuthRepository>().InitSchema();
        }
    
        public void Configure(IServiceCollection services)
        {
            services.AddSingleton<IPaygateConnectionFactory>(new PaygateConnectionFactory(
                Configuration.GetConnectionString("Paygate"),
                SqlServer2014OrmLiteDialectProvider.Instance));
        }
    }
}