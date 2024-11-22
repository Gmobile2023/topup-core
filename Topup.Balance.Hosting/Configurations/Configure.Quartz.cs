using System;
using Topup.Balance.Components.Jobs;
using Topup.Balance.Hosting.Configurations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

[assembly: HostingStartup(typeof(ConfigureQuartz))]

namespace Topup.Balance.Hosting.Configurations;

public class ConfigureQuartz : IHostingStartup
{
    public void Configure(IWebHostBuilder builder)
    {
        builder.ConfigureServices((context, services) =>
        {
            // base configuration from appsettings.json
            // services.Configure<Quartz.QuartzOptions>(context.Configuration.GetSection("Quartz"));
            //
            // // if you are using persistent job store, you might want to alter some options
            // services.Configure<Quartz.QuartzOptions>(options =>
            // {
            //     options.Scheduling.IgnoreDuplicates = true; // default: false
            //     options.Scheduling.OverWriteExistingData = true; // default: true
            // });

            // services.AddTransient<AutoCheckTransJob>();
            // services.AddTransient<CheckKppAccountNotWorkingJob>();

            services.AddQuartz(q =>
            {
                // handy when part of cluster or you want to otherwise identify multiple schedulers
                q.SchedulerId = "Balance_Quartz";
                q.SchedulerId = "AUTO";

                // or for scoped service support like EF Core DbContext
                q.UseMicrosoftDependencyInjectionJobFactory();

                // these are the defaults
                q.UseSimpleTypeLoader();
                q.UseInMemoryStore();
                q.UseDefaultThreadPool(tp => { tp.MaxConcurrency = 10; });
                q.UseTimeZoneConverter();
                
                // var jobKey = new JobKey("Auto check trans", "AutoCheck");

                // q.AddJob<AutoCheckTransJob>(jobKey, j => { j.WithDescription("Auto check transaction KPP"); });
                // q.AddTrigger(t => t
                //         .WithIdentity("trigger1")
                //         .StartNow()
                //         .ForJob(jobKey)
                //         .WithSimpleSchedule(x => x.WithInterval(TimeSpan.FromMinutes(15)).RepeatForever())
                //         //.WithSchedule(CronScheduleBuilder.DailyAtHourAndMinute(6, 0))
                // );
                //
                var jobKey = new JobKey("System", "AutoTransfer");
                
                q.AddJob<SystemTransferJob>(jobKey, j => { j.WithDescription("Auto transfer system account"); });
                q.AddTrigger(t => t
                        .WithIdentity("trigger1")
                        .ForJob(jobKey)
                        .StartNow()
                        .WithCronSchedule("0 00 04 ? * *")
                        // .WithSimpleSchedule(x => x.WithInterval(TimeSpan.FromMinutes(3)).RepeatForever())
                        .WithDescription("Auto system account transfer")
                );

                
            });


            // // Quartz.Extensions.Hosting allows you to fire background service that handles scheduler lifecycle
            services.AddQuartzHostedService(options =>
            {
                // when shutting down we want jobs to complete gracefully
                options.WaitForJobsToComplete = true;
            });
        });
    }
}