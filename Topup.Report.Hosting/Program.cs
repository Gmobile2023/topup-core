using System.IO;
using System.Net;
using Topup.Shared;
using Infrastructure.AppVersion;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using ServiceStack;

ServiceStackHelper.SetLicense();

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseKestrel(opt =>
{
    opt.Limits.MaxRequestBodySize = 100 * 1024 * 1024;
}).UseContentRoot(Directory.GetCurrentDirectory());

builder.Logging.AddSerilog();

var app = builder.Build();


if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    app.UseHttpsRedirection();
    //app.Services.Resolve<AlarmAppVersion>().AlarmVersion();
}

app.Run();