using System.Net;
using Topup.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Serilog;

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
app.Run();