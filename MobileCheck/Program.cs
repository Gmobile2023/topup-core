using System;
using System.Net;
using GMB.Topup.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using MobileCheck.Data;
using Serilog;
using Polly;

ServiceStackHelper.SetLicense();
ServicePointManager.ServerCertificateValidationCallback +=
    (_, _, _, _) => true;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddSerilog();

var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.Lifetime.ApplicationStarted.Register(async () =>
{
    await Policy.Handle<TimeoutException>()
        .WaitAndRetryAsync(5, retryAttempt => TimeSpan.FromSeconds(10))
        .ExecuteAndCaptureAsync(async () =>  await DbInitializer.InitDb(app));

});
app.Run();