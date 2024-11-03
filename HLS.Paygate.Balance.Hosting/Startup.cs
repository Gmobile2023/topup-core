using HLS.Paygate.Balance.Components.Services;
using HLS.Paygate.Balance.Domain.Repositories;
using HLS.Paygate.Balance.Domain.Services;
using HLS.Paygate.Shared.Helpers;
using HLS.Paygate.Shared.UniqueIdGenerator;
using Infrastructure.AppVersion;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using ServiceStack;

namespace HLS.Paygate.Balance.Hosting;

public class Startup : ModularStartup
{
    public Startup(IConfiguration configuration) : base(configuration)
    {
        Configuration = configuration;
    }

    // public Startup(IConfiguration configuration)//, IWebHostEnvironment env)
    // {
    //     // HostingEnvironment = env;
    //     Configuration = configuration;
    // }
    //
    // private IConfiguration Configuration { get; }

    // This method gets called by the runtime. Use this method to add services to the container.
    // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
    public new void ConfigureServices(IServiceCollection services)
    {
        // var serviceConfig = Configuration.GetServiceConfig();
        // services.RegisterRedisSentinel(serviceConfig);

        services.AddScoped<ITransCodeGenerator, TransCodeGenerator>();
        services.AddTransient<MainService>();
        services.AddScoped<IBalanceService, BalanceService>();
        services.AddScoped<ITransactionService, TransactionService>();
        services.AddScoped<IBalanceMongoRepository, BalanceMongoRepository>();
        //services.AddScoped<ITransactionReportService, TransactionReportService>();
        services.AddTransient<AlarmAppVersion>();
        services.AddTransient<GrpcClientHepper>(); 
        // services.AddTransient<MainService>();
        // services.AddSingleton<IMongoDbContext>(p =>
        //     new MongoDbContext(Configuration.GetConnectionString("Mongodb"), Configuration["ConnectionStrings:MongoDatabaseName"]));
        //services.AddScoped<ISaleRepository, SaleRepository>();

        // services.AddMassTransit(x =>
        // {
        //     // add the consumer to the container
        //     x.AddConsumer<BalanceConsumer>();
        //     x.AddConsumer<PaymentConsumer>();
        //     // x.AddConsumer<CollectDiscountConsumer>();
        //     x.AddConsumer<CancelPaymentConsumer>();
        //     x.AddConsumer<PriorityPaymentConsumer>();
        // });
        //
        // // services.AddClusterService();
        //
        // services.AddSingleton(provider => Bus.Factory.CreateUsingRabbitMq(cfg =>
        // {
        //     cfg.Host(Configuration["RabbitMq:Host"], Configuration["RabbitMq:VirtualHost"], h =>
        //     {
        //         h.Username(Configuration["RabbitMq:Username"]);
        //         h.Password(Configuration["RabbitMq:Password"]);
        //     });
        //
        //     cfg.ReceiveEndpoint(KebabCaseEndpointNameFormatter.Instance.Consumer<BalanceConsumer>(), e =>
        //     {
        //         e.PrefetchCount = 16;
        //         //e.UseMessageRetry(x => x.Interval(2, 100));
        //         e.Consumer<BalanceConsumer>(provider);
        //     });
        //
        //     cfg.ReceiveEndpoint(KebabCaseEndpointNameFormatter.Instance.Consumer<PaymentConsumer>(), e =>
        //     {
        //         e.PrefetchCount = 16;
        //         //e.UseMessageRetry(x => x.Interval(2, 100));
        //         e.Consumer<PaymentConsumer>(provider);
        //     });
        //     // cfg.ReceiveEndpoint(KebabCaseEndpointNameFormatter.Instance.Consumer<CollectDiscountConsumer>(), e =>
        //     // {
        //     //     e.PrefetchCount = 16;
        //     //     //e.UseMessageRetry(x => x.Interval(2, 100));
        //     //     e.Consumer<CollectDiscountConsumer>(provider);
        //     // });
        //     cfg.ReceiveEndpoint(KebabCaseEndpointNameFormatter.Instance.Consumer<CancelPaymentConsumer>(), e =>
        //     {
        //         e.PrefetchCount = 16;
        //        // e.UseMessageRetry(x => x.Interval(2, 100));
        //         e.Consumer<CancelPaymentConsumer>(provider);
        //     });
        //     cfg.ReceiveEndpoint(KebabCaseEndpointNameFormatter.Instance.Consumer<PriorityPaymentConsumer>(), e =>
        //     {
        //         e.PrefetchCount = 16;
        //         //e.UseMessageRetry(x => x.Interval(2, 100));
        //         e.Consumer<PriorityPaymentConsumer>(provider);
        //     });
        // }));
        //
        // services.AddSingleton<IPublishEndpoint>(provider => provider.GetRequiredService<IBusControl>());
        // services.AddSingleton<ISendEndpointProvider>(provider => provider.GetRequiredService<IBusControl>());
        // services.AddSingleton<IBus>(provider => provider.GetRequiredService<IBusControl>());
        // services.AddSingleton<IHostedService, MassTransitApiHostedService>();
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory,
        AlarmAppVersion version)
    {
        if (!env.IsDevelopment()) version.AlarmVersion();

        var appHost = new AppHost(Configuration)
        {
            AppSettings = new NetCoreAppSettings(Configuration)
        };
        loggerFactory.AddSerilog();
        //loggerFactory.AddNLog();
        //loggerFactory.AddProvider(new NLog.Extensions.Logging.NLogLoggerProvider());
        app.UseServiceStack(appHost);
    }
}