using System;
using System.Threading.Tasks;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.ConfigDtos;
using HLS.Paygate.Shared.Helpers;
using Microsoft.Extensions.Configuration;
using MobileCheck.Models;
using Paygate.Discovery.Requests.TopupGateways;

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

    public async Task<(NewMessageReponseBase<string>, MobileInfo)> CheckMobile(string transCode, MobileInfo mobileInfo)
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
                check.ResponseStatus.ErrorCode = "00";//convert lại mã TC

            return (check, mobileInfo);
        }
        catch (Exception e)
        {
            return (new NewMessageReponseBase<string>()
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