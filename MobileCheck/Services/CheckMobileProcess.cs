using System;
using System.Linq;
using System.Threading.Tasks;
using GMB.Topup.Shared;
using MassTransit;
using Microsoft.Extensions.Configuration;
using MobileCheck.Services;
using MongoDB.Entities;
using ServiceStack;

namespace MobileCheck.Services;

public class CheckMobileProcess
{
    readonly CheckMobileHttpClient _client;
    private readonly IConfiguration _configuration;

    public CheckMobileProcess(CheckMobileHttpClient client, IConfiguration configuration)
    {
        _client = client;
        _configuration = configuration;
    }

    public async Task CheckMobileJob()
    {
        Console.WriteLine($"{DateTime.Now:dd/MM/yyyy HH:mm:ss}-CheckMobileJob processing");
        var res = await DB.PagedSearch<MobileCheck.Models.MobileInfo>()
            .Match(x => x.LastCheckDate < DateTime.Now.AddDays(-int.Parse(_configuration["DaysFromLastCheck"])))
            .Sort(b => b.LastCheckDate, Order.Ascending)
            .PageSize(10)
            .PageNumber(1)
            .ExecuteAsync();

        var mobileInfos = res.Results;

        if (mobileInfos.Count > 0)
        {
            foreach (var mobileInfo in mobileInfos)
            {
                mobileInfo.Index = DateTime.Now.ToString("ddMMyyyy");
                var result = await _client.CheckMobile(NewId.NextGuid().ToString(), mobileInfo);
                Console.WriteLine(
                    $"{DateTime.Now:dd/MM/yyyy HH:mm:ss}-CheckMobileJob item {mobileInfo.Mobile} return {result.Item1.ToJson()}");
                if (result.Item1.ResponseStatus.ErrorCode != "9999" && result.Item2 != null)
                {
                    await result.Item2.SaveAsync();
                }
            }
        }
    }

    public async Task<NewMessageResponseBase<string>> CheckMobile(string mobile, string telco)
    {
        var mobileInfos = await DB.Find<MobileCheck.Models.MobileInfo>().Match(p => p.Mobile == mobile).ExecuteAsync();

        var mobileInfo = mobileInfos.FirstOrDefault();

        if (mobileInfo != null)
        {
            if (mobileInfo.LastCheckDate > DateTime.Now.AddDays(-int.Parse(_configuration["DaysFromLastCheck"])))
            {
                if (!string.IsNullOrEmpty(mobileInfo.MobileType) && mobileInfo.MobileType != "FAIL")
                {
                    if (mobileInfo.MobileType == "NOT_VALID")
                    {
                        return new NewMessageResponseBase<string>
                        {
                            Results = "NOT_VALID",
                            ResponseStatus = new ResponseStatusApi()
                            {
                                ErrorCode = ResponseCodeConst.Success,
                                Message = "Fail"
                            }
                        };
                    }

                    return new NewMessageResponseBase<string>
                    {
                        Results = mobileInfo?.Telco + "|" + mobileInfo?.MobileType + "|" + "SYSTEM",
                        ResponseStatus = new ResponseStatusApi()
                        {
                            ErrorCode = ResponseCodeConst.Error,
                            Message = "Success"
                        }
                    };
                }
            }
        }
        else
        {
            mobileInfo = new MobileCheck.Models.MobileInfo
            {
                Mobile = mobile,
                Telco = telco,
                CreatedAt = DateTime.Now,
                CheckerProvider = "VIETTEL2",
                Index = DateTime.Now.ToString("ddMMyyyy")
            };
        }

        var result1 = await _client.CheckMobile(NewId.NextGuid().ToString(), mobileInfo);

        if (result1.Item1.ResponseStatus.ErrorCode != "9999" && result1.Item2 != null)
        {
            await result1.Item2.SaveAsync();
        }

        return result1.Item1;
    }
}