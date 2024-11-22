using System;
using System.Threading.Tasks;
using Topup.Shared;
using Topup.Shared.ConfigDtos;
using Topup.Shared.Helpers;
using Microsoft.Extensions.Configuration;
using MobileCheck.Models;
using Topup.Discovery.Requests.TopupGateways;

namespace MobileCheck.Services;

public class CheckMobileHttpClient
{
    private readonly GrpcClientHepper _grpcClient;
    private readonly IConfiguration _config;

    public CheckMobileHttpClient(GrpcClientHepper grpcClient, IConfiguration config)
    {
        _grpcClient = grpcClient;
        _config = config;
    }

    public async Task<(NewMessageResponseBase<string>, MobileInfo)> CheckMobile(string transCode, MobileInfo mobileInfo)
    {
        try
        {
            var check = await _grpcClient.GetClientCluster(GrpcServiceName.TopupGateway, 5).SendAsync(
                new GateCheckMsIsdnRequest()
                {
                    MsIsdn = mobileInfo.Mobile,
                    TransCode = transCode,
                    ProviderCode = mobileInfo.CheckerProvider,
                    Telco = mobileInfo.Telco
                });
            if (check.ResponseStatus.ErrorCode != ResponseCodeConst.Success || string.IsNullOrEmpty(check.Results))
            {
                mobileInfo.MobileType = "FAIL";
                mobileInfo.LastCheckDate = DateTime.Now;
            }

            if (!check.Results.Contains("TT") && !check.Results.Contains("TS"))
            {
                mobileInfo.MobileType = "NOT_VALID";
                mobileInfo.LastCheckDate = DateTime.Now;
            }
            else
            {
                mobileInfo.MobileType = check.Results.Split('|')[1];
                mobileInfo.Telco = check.Results.Split('|')[0];
                mobileInfo.LastCheckDate = DateTime.Now;
            }

            if (check.ResponseStatus.ErrorCode == ResponseCodeConst.Success)
                check.ResponseStatus.ErrorCode = ResponseCodeConst.Error;//convert lại mã TC

            return (check, mobileInfo);
        }
        catch (Exception e)
        {
            return (new NewMessageResponseBase<string>()
            {
                ResponseStatus = new ResponseStatusApi()
                {
                    ErrorCode = "9999",
                    Message = e.Message
                }
            }, mobileInfo);
        }
    }
}