using System;
using System.Collections;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Topup.Shared;
using Topup.Shared.Dtos;
using Topup.Shared.Utils;
using Topup.TopupGw.Contacts.ApiRequests;
using Topup.TopupGw.Contacts.Dtos;
using Topup.TopupGw.Contacts.Enums;
using Topup.TopupGw.Domains.BusinessServices;
using Microsoft.Extensions.Logging;
using ServiceStack;

namespace Topup.TopupGw.Components.Connectors.MTC;

public class MtcConnector : IGatewayConnector
{
    private readonly ILogger<MtcConnector> _logger;
    private readonly ITopupGatewayService _topupGatewayService;

    public MtcConnector(ITopupGatewayService topupGatewayService, ILogger<MtcConnector> logger)
    {
        _topupGatewayService = topupGatewayService;
        _logger = logger;
    }

    public async Task<MessageResponseBase> TopupAsync(TopupRequestLogDto topupRequestLog, ProviderInfoDto providerInfo)
    {
        _logger.Log(LogLevel.Information, "MtcConnector topup request: " + topupRequestLog.ToJson());
        var responseMessage = new MessageResponseBase();
        try
        {
            if (!_topupGatewayService.ValidConnector(ProviderConst.MTC, providerInfo.ProviderCode))
            {
                _logger.LogError(
                    $"{topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{providerInfo.ProviderCode}-MtcConnector ProviderConnector not valid");
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                };
            }

            var client = new JsonHttpClient(providerInfo.ApiUrl)
            {
                HttpClient = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(providerInfo.Timeout),
                    BaseAddress = new Uri(providerInfo.ApiUrl)
                }
            }; //("http://dev.api.zo-ta.com");
            var request = new MtcRequest
            {
                Username = providerInfo.Username,
                Privatekey = providerInfo.Password,
                Phone = topupRequestLog.ReceiverInfo,
                Sotien = topupRequestLog.TransAmount,
                Transidclient = topupRequestLog.TransCode,
                Gateway = topupRequestLog.Vendor
            };
            responseMessage.TransCodeProvider = topupRequestLog.TransCode;
            //Correct for call to partner
            if (topupRequestLog.Vendor == "VTE")
                request.Gateway = "VTL";
            if (topupRequestLog.Vendor == "VNA")
                request.Gateway = "VNP";
            if (topupRequestLog.Vendor == "VNM")
                request.Gateway = "VNB";
            if (topupRequestLog.Vendor == "GMOBILE")
                request.Gateway = "GTEL";

            try
            {
                var hash = Cryptography.HashSHA1(string.Join("", request.Username, request.Phone, request.Sotien,
                    request.Privatekey));

                request.Checksum = hash;
            }
            catch (Exception e)
            {
                _logger.LogError("Error sign data: " + e.Message);
                topupRequestLog.Status = TransRequestStatus.Fail;
                await _topupGatewayService.TopupRequestLogUpdateAsync(topupRequestLog);
            }

            _logger.LogInformation("MtcConnector topup send: " + request.ToJson());
            //The operation has timed out.

            string result = null;
            try
            {
                var urlGet = "/apipayment/topup.jsp" +
                             request.ToGetUrl().Replace("/json/reply/MtcRequest", string.Empty);
                _logger.LogInformation("Topup url: " + urlGet);
                result = await client.GetAsync<string>(urlGet);
            }
            catch (Exception ex)
            {
                _logger.LogError("MtcConnector exception: " + ex.Message);
                if (ex.Message.ToLower().Contains("timeout")) result = "501102";
            }

            if (!string.IsNullOrEmpty(result))
            {
                var resultCode = result.Split(';')[0];

                topupRequestLog.ModifiedDate = DateTime.Now;
                topupRequestLog.ResponseInfo = result;

                _logger.LogInformation(
                    $"MtcConnector topup return:{topupRequestLog.ProviderCode}-{topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{result}");
                if (resultCode == "1")
                {
                    topupRequestLog.Status = TransRequestStatus.Success;
                    responseMessage.ResponseCode = ResponseCodeConst.Success;
                    responseMessage.ResponseMessage = "Giao dịch thành công";
                }
                else if (((IList)new[] { "-1", "-99", "501102" }).Contains(resultCode))
                {
                    //var reResult =await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.MTC, resultCode,topupRequestLog.TransCode);
                    topupRequestLog.Status = TransRequestStatus.Timeout;
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage =
                        "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                }
                else
                {
                    var reResult =
                        await _topupGatewayService.GetResponseMassageCacheAsync("MTC", resultCode,
                            topupRequestLog.TransCode);
                    topupRequestLog.Status = TransRequestStatus.Fail;
                    responseMessage.ResponseCode = reResult != null ? reResult.ResponseCode : ResponseCodeConst.Error;
                    responseMessage.ResponseMessage =
                        reResult != null ? reResult.ResponseName : "Giao dịch không thành công từ nhà cung cấp";
                }

                responseMessage.ProviderResponseCode = resultCode;
                responseMessage.ProviderResponseMessage = responseMessage.ResponseMessage;
            }
            else
            {
                _logger.LogInformation("Error send request");
                responseMessage.ResponseMessage = "Lỗi kết nối nhà cung cấp";
                topupRequestLog.Status = TransRequestStatus.Fail;
            }

            await _topupGatewayService.TopupRequestLogUpdateAsync(topupRequestLog);
        }
        catch (Exception e)
        {
            _logger.LogError($"{topupRequestLog.TransCode} -{topupRequestLog.TransRef} Topup error {e}");
            responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
            responseMessage.ResponseMessage =
                "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
        }


        return responseMessage;
    }

    public async Task<MessageResponseBase> TransactionCheckAsync(string providerCode, string transCodeToCheck,
        string transCode, string serviceCode = null, ProviderInfoDto providerInfo = null)
    {
        try
        {
            _logger.LogInformation($"{transCodeToCheck} MtcConnector check request: " + transCode);
            var responseMessage = new MessageResponseBase();

            if (providerInfo == null)
                providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(providerCode);

            if (providerInfo == null ||
                !_topupGatewayService.ValidConnector(ProviderConst.MTC, providerInfo.ProviderCode))
            {
                _logger.LogError(
                    $"{transCode}-{providerCode}-{providerCode}-MtcConnector ProviderConnector not valid");
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult,
                    ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ"
                };
            }

            var client = new JsonHttpClient(providerInfo.ApiUrl)
            {
                HttpClient = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(providerInfo.Timeout),
                    BaseAddress = new Uri(providerInfo.ApiUrl)
                }
            }; //("http://dev.api.zo-ta.com");

            var request = new MtcRequest
            {
                Username = providerInfo.Username,
                Privatekey = providerInfo.Password,
                Transidclient = transCodeToCheck
            };
            // var hash = Cryptography.HashSHA1(string.Join("|", request.Username, ":::", request.Privatekey));
            // request.Checksum = hash;

            _logger.LogInformation($"{transCodeToCheck} MtcConnector check send: " + request.ToJson());

            string result = null;
            try
            {
                var urlGet = "/apipayment/recheck.jsp" +
                             request.ToGetUrl().Replace("/json/reply/MtcRequest", string.Empty);
                _logger.LogInformation("CheckTrans url: " + urlGet);
                result = await client.GetAsync<string>(urlGet);
            }
            catch (Exception ex)
            {
                _logger.LogError($"{transCodeToCheck} MtcConnector check exception: " + ex.Message);
                result = "501102";
            }

            if (result != null)
            {
                _logger.LogInformation(
                    $"{providerCode}-{transCodeToCheck} MtcConnector check return: {transCode}-{transCodeToCheck}-{result}");
                var resultCode = result.Split(';')[0];
                //responseMessage.ExtraInfo = string.Join("|", resultCode, null);
                if (resultCode == "1")
                {
                    responseMessage.ResponseCode = ResponseCodeConst.Success;
                    responseMessage.ResponseMessage = "Giao dịch thành công";
                    //   responseMessage.Payload = result.Transaction;
                }
                else if (((IList)new[] { "-1", "-99", "501102" }).Contains(resultCode))
                {
                    //var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.MTC, resultCode, transCodeToCheck);
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage =
                        "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                }
                else
                {
                    //var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.MTC, resultCode, transCodeToCheck);
                    responseMessage.ResponseCode = ResponseCodeConst.Error;
                    responseMessage.ResponseMessage = "Giao dịch không thành công từ nhà cung cấp";
                }

                responseMessage.ProviderResponseCode = resultCode;
                responseMessage.ProviderResponseMessage = responseMessage.ResponseMessage;
            }
            else
            {
                _logger.LogInformation($"{transCodeToCheck} Error send request");
                responseMessage.ResponseMessage =
                    "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_TimeOut;
            }

            return responseMessage;
        }
        catch (Exception ex)
        {
            return new MessageResponseBase
            {
                ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ",
                ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult,
                Exception = ex.Message
            };
        }
    }

    public async Task<NewMessageResponseBase<InvoiceResultDto>> QueryAsync(PayBillRequestLogDto payBillRequestLog)
    {
        //_logger.Log(LogLevel.Information, $"{payBillRequestLog.TransCode} MtcConnector query request: " + payBillRequestLog.ToJson());
        var responseMessage = new NewMessageResponseBase<InvoiceResultDto>
        {
            ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Truy vấn thông tin không thành công")
        };
        var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(payBillRequestLog.ProviderCode);

        if (providerInfo == null)
            return responseMessage;

        if (!_topupGatewayService.ValidConnector(ProviderConst.MTC, providerInfo.ProviderCode))
        {
            _logger.LogError(
                $"{payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{providerInfo}-MtcConnector ProviderConnector not valid");
            responseMessage.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error,
                "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ");
            return responseMessage;
        }

        var client = new JsonHttpClient(providerInfo.ExtraInfo)
        {
            HttpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(providerInfo.Timeout),
                BaseAddress = new Uri(providerInfo.ApiUrl)
            }
        }; //("http://dev.api.zo-ta.com");


        var request = new MtcRequest
        {
            Username = providerInfo.ApiUser,
            Password = providerInfo.ApiPassword
        };

        _logger.LogInformation($"{payBillRequestLog.TransCode} MtcConnector query send: " + request.ToJson());

        string result = null;
        try
        {
            var urlGet = "/check/login.jsp" + request.ToGetUrl().Replace("/json/reply/MtcRequest", string.Empty);
            _logger.LogInformation($"{payBillRequestLog.TransCode} Login url: " + urlGet);
            result = await client.GetAsync<string>(urlGet);

            if (!string.IsNullOrEmpty(result))
            {
                result = result.Replace("\n", string.Empty).Replace("\r", string.Empty);
                _logger.Log(LogLevel.Information,
                    $"Query Login return: {payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{result}");
                if (result != "-1")
                {
                    request = new MtcRequest
                    {
                        Sha = result,
                        Phone = payBillRequestLog.ReceiverInfo
                    };
                    urlGet = "/check/nocuoc.jsp" +
                             request.ToGetUrl().Replace("/json/reply/MtcRequest", string.Empty);
                    _logger.LogInformation("Query url: " + urlGet);
                    result = await client.GetAsync<string>(urlGet);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"{payBillRequestLog.TransCode} MtcConnector Query exception: " + ex.Message);
            result = "501102";
        }

        if (!string.IsNullOrEmpty(result))
        {
            result = result.Replace("\n", string.Empty).Replace("\r", string.Empty);
            _logger.Log(LogLevel.Information,
                $"{payBillRequestLog.ProviderCode}-{payBillRequestLog.TransCode} MtcConnector Query return: {request.Transidclient}-{result}");
            if (result == "-7")
            {
                responseMessage.ResponseStatus.Message = "Số điện thoại không phải là Mobifone trả sau.";
            }
            else if (result == "-4")
            {
                responseMessage.ResponseStatus.Message = "Số điện thoại không thuộc nhà mạng Mobifone.";
            }
            else if (result == "-3")
            {
                responseMessage.ResponseStatus.Message = "Sai định dạng số điện thoại";
            }
            else if (new[] { "-6", "-5", "-1", "-2" }.Contains(result))
            {
                responseMessage.ResponseStatus.Message = "Truy vấn không thành công";
            }
            else
            {
                var dto = new InvoiceResultDto
                {
                    Amount = decimal.Parse(result)
                };

                responseMessage.ResponseStatus.ErrorCode = ResponseCodeConst.Success;
                responseMessage.ResponseStatus.Message = "Giao dịch thành công";
                responseMessage.Results = dto;
            }
            //responseMessage.ProviderResponseCode = result;
            //responseMessage.ProviderResponseMessage = responseMessage.ResponseMessage;
        }
        else
        {
            _logger.LogInformation($"{payBillRequestLog.TransCode} Error send request");
            responseMessage.ResponseStatus.Message = "Lỗi kết nối nhà cung cấp";
        }

        return responseMessage;
    }

    public Task<MessageResponseBase> CardGetByBatchAsync(CardRequestLogDto cardRequestLog)
    {
        throw new NotImplementedException();
    }

    public async Task<MessageResponseBase> CheckBalanceAsync(string providerCode, string transCode)
    {
        _logger.Log(LogLevel.Information, $"{transCode} Get balance request: " + transCode);
        var responseMessage = new MessageResponseBase();

        var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(providerCode);

        if (providerInfo == null)
            return responseMessage;

        if (!_topupGatewayService.ValidConnector(ProviderConst.MTC, providerInfo.ProviderCode))
        {
            _logger.LogError($"{providerCode}-{transCode}-{providerCode}-MtcConnector ProviderConnector not valid");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
            };
        }


        var client = new JsonHttpClient(providerInfo.ApiUrl)
        {
            HttpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(providerInfo.Timeout),
                BaseAddress = new Uri(providerInfo.ApiUrl)
            }
        }; //("http://dev.api.zo-ta.com");

        var request = new MtcRequest
        {
            Username = providerInfo.Username,
            Privatekey = providerInfo.Password
        };

        var hash = Cryptography.HashSHA1(string.Join("", request.Username, ":::", request.Privatekey));

        request.Checksum = hash;

        _logger.LogInformation($"{transCode} Balance object send: " + request);

        string result = null;
        try
        {
            var urlGet = "/apipayment/userbalance.jsp" +
                         request.ToGetUrl().Replace("/json/reply/MtcRequest", string.Empty);
            _logger.LogInformation($"{transCode} Balance url: " + urlGet);
            result = await client.GetAsync<string>(urlGet);
        }
        catch (Exception ex)
        {
            _logger.LogError($"{transCode} Balance exception: " + ex.Message);
            result = "501102";
        }

        if (result != null)
        {
            _logger.Log(LogLevel.Information, $"{providerCode} Balance return: {transCode}-{result}");
            var resultCode = result.Split(';')[0];

            responseMessage.ResponseCode = ResponseCodeConst.Success;
            responseMessage.ResponseMessage = "Giao dịch thành công";
            responseMessage.Payload = resultCode;
        }
        else
        {
            _logger.LogInformation($"{transCode} Error send request");
            responseMessage.ResponseMessage = "Lỗi kết nối nhà cung cấp";
        }

        return responseMessage;
    }

    public Task<MessageResponseBase> DepositAsync(DepositRequestDto request)
    {
        throw new NotImplementedException();
    }

    public Task<MessageResponseBase> PayBillAsync(PayBillRequestLogDto payBillRequestLog)
    {
        throw new NotImplementedException();
    }

    public Task<ResponseMessageApi<object>> GetProductInfo(GetProviderProductInfo info)
    {
        throw new NotImplementedException();
    }

    public Task<MessageResponseBase> CheckPrePostPaid(string msisdn, string transCode, string providerCode)
    {
        throw new NotImplementedException();
    }
}

public class MtcRequest
{
    public string Username { get; set; }
    public string Privatekey { get; set; }
    public string Password { get; set; }
    public string Phone { get; set; }
    public int Sotien { get; set; }
    public string Checksum { get; set; }
    public string Transidclient { get; set; }
    public string Gateway { get; set; }
    public string Sha { get; set; }
}