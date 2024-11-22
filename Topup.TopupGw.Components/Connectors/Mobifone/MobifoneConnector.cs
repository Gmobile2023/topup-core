using Topup.Shared;
using Topup.Shared.CacheManager;
using Topup.Shared.Dtos;
using Topup.TopupGw.Components.Connectors.ESale;



using Topup.TopupGw.Domains.BusinessServices;
using MassTransit;
using Microsoft.Extensions.Logging;
using MobifoneService;
using Org.BouncyCastle.Asn1.X509;
using ServiceStack;
using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Topup.TopupGw.Contacts.ApiRequests;
using Topup.TopupGw.Contacts.Dtos;
using Topup.TopupGw.Contacts.Enums;

namespace Topup.TopupGw.Components.Connectors.Mobifone;

public class MobifoneConnector : IGatewayConnector
{
    private readonly IBusControl _bus;
    private readonly ILogger<MobifoneConnector> _logger;
    private readonly ITopupGatewayService _topupGatewayService;
    private readonly ICacheManager _cacheManager;

    public MobifoneConnector(ITopupGatewayService topupGatewayService, ILogger<MobifoneConnector> logger,
        IBusControl bus, ICacheManager cacheManager)
    {
        _topupGatewayService = topupGatewayService;
        _logger = logger;
        _bus = bus;
        _cacheManager = cacheManager;
    }

    public async Task<MessageResponseBase> TopupAsync(TopupRequestLogDto topupRequestLog, ProviderInfoDto providerInfo)
    {
        _logger.LogInformation($"{topupRequestLog.TransCode} MobifoneConnector topup request: " +
                               topupRequestLog.ToJson());
        var responseMessage = new MessageResponseBase();
        try
        {
            if (!_topupGatewayService.ValidConnector(ProviderConst.MOBIFONE, providerInfo.ProviderCode))
            {
                _logger.LogError(
                    $"{topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{providerInfo.ProviderCode}-MobifoneConnector ProviderConnector not valid");
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                };
            }

            //string target = topupRequestLog.ProductCode.StartsWith("VMS_BILL") ? "postpaid" : "airtime";
            var target = providerInfo.ProviderCode == ProviderConst.MOBIFONE ? "airtime" : "postpaid";
            responseMessage.TransCodeProvider = topupRequestLog.TransCode;
            var topupRequest = new MobifoneRequest()
            {
                Account = providerInfo.Username,
                Password = providerInfo.Password,
                Amount = topupRequestLog.TransAmount,
                Recipient = topupRequestLog.ReceiverInfo,
                Reference1 = topupRequestLog.TransRef,
                Reference2 = topupRequestLog.TransCode,
                TransType = MobifoneRequest.Type_Topup,
                Target = target
            };

            _logger.LogInformation(
                $"{topupRequestLog.TransCode} MOBIFONEConnector_login_send: " + topupRequest.ToJson());
            var response = await CallApi(topupRequest, providerInfo);
            if (response != null)
            {
                topupRequestLog.ResponseInfo = response.ToJson();
                topupRequestLog.ModifiedDate = DateTime.Now;
                responseMessage.ProviderResponseCode = response?.Result.ToString();
                responseMessage.ProviderResponseMessage = response?.ResultNamespace;
                _logger.LogInformation(
                    $"{topupRequestLog.ProviderCode}{topupRequestLog.TransCode} MobifoneConnector_return: {topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{response.ToJson()}");
                if (response.Result.ToString() == "0")
                {
                    topupRequestLog.Status = TransRequestStatus.Success;
                    responseMessage.ResponseCode = ResponseCodeConst.Success;
                    responseMessage.ResponseMessage = "Giao dịch thành công";
                    responseMessage.ProviderResponseTransCode = response.TransId.ToString();
                }
                else if (new[] { "5000", "501102" }.Contains(response.Result.ToString()))
                {
                    //var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.MOBIFONE, response.Result.ToString(), topupRequestLog.TransCode);
                    topupRequestLog.Status = TransRequestStatus.Timeout;
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả xử lý.";
                }
                else if (providerInfo.ExtraInfo.Contains(response.Result.ToString()))
                {
                    var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.MOBIFONE,
                        response.Result.ToString(), topupRequestLog.TransCode);
                    topupRequestLog.Status = TransRequestStatus.Fail;
                    responseMessage.ResponseCode = responseMessage.ResponseCode =
                        reResult != null ? reResult.ResponseCode : ResponseCodeConst.Error;
                    responseMessage.ResponseMessage =
                        reResult != null ? reResult.ResponseName : "Giao dịch không thành công";
                }
                else
                {
                    var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.MOBIFONE,
                        response.Result.ToString(), topupRequestLog.TransCode);
                    topupRequestLog.Status = TransRequestStatus.Fail;
                    responseMessage.ResponseCode = reResult != null ? reResult.ResponseCode : ResponseCodeConst.Error;
                    responseMessage.ResponseMessage = reResult != null
                        ? reResult.ResponseName
                        : response.ResultNamespace ?? "Giao dịch không thành công";
                }
            }
            else
            {
                _logger.LogInformation($"{topupRequestLog.TransCode} Login Error");
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
            _logger.LogInformation(
                $"{transCodeToCheck}-{transCode}-{providerCode} MobifoneConnector check request: " + transCode);

            var responseMessage = new MessageResponseBase();

            if (providerInfo == null)
                providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(providerCode);

            if (providerInfo == null ||
                !_topupGatewayService.ValidConnector(ProviderConst.MOBIFONE, providerInfo.ProviderCode))
            {
                _logger.LogError(
                    $"{transCodeToCheck}-{transCode}-{providerCode}-MobifoneConnector ProviderConnector not valid");
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult,
                    ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ"
                };
            }

            var topupRequestLog =
                await _topupGatewayService.GetTopupRequestLogAsync(transCodeToCheck, ProviderConst.MOBIFONE);
            if (topupRequestLog != null)
            {
                var checkTransRequest = new MobifoneRequest()
                {
                    Account = providerInfo.Username,
                    Password = providerInfo.Password,
                    Recipient = topupRequestLog.ReceiverInfo,
                    Reference1 = transCode,
                    Reference2 = transCodeToCheck,
                    TransType = MobifoneRequest.Type_Checktrans
                };

                _logger.LogInformation($"{transCodeToCheck} - {transCode} MOBIFONEConnector checktrans request: " +
                                       checkTransRequest.ToJson());
                var response = await CallApi(checkTransRequest, providerInfo);
                _logger.Log(LogLevel.Information,
                    $"{providerCode}-{transCodeToCheck} MobifoneConnector checktrans return: {transCode}-{transCodeToCheck}-{response.ToJson()}");

                if (response.Result.ToString() == "0" && response.Detail != null)
                {
                    responseMessage.ResponseCode = response.Detail == "TC" ? ResponseCodeConst.Success :
                        response.Detail.StartsWith("TC") ? ResponseCodeConst.ResponseCode_WaitForResult :
                        ResponseCodeConst.Error;
                    responseMessage.ResponseMessage = response.Detail == "TC" ? "Giao dịch thành công" :
                        response.Detail.StartsWith("TC") ? response.Detail : response.Detail;
                    responseMessage.ProviderResponseTransCode = response.TransId.ToString();
                    if (response.ChecktransDetail != null)
                    {
                        responseMessage.ReceiverType = response.ChecktransDetail.TransType switch
                        {
                            "PREPAYID" => "TT",
                            "POSTPAID" => "TS",
                            _ => responseMessage.ReceiverType
                        };
                    }
                }
                else
                {
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage =
                        "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                }

                responseMessage.ProviderResponseCode = response.Result.ToString();
                responseMessage.ProviderResponseMessage = response.Detail;
            }
            else
            {
                return responseMessage;
            }

            return responseMessage;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return new MessageResponseBase
            {
                ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ",
                ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult,
                Exception = e.Message
            };
        }
    }

    public async Task<NewMessageResponseBase<InvoiceResultDto>> QueryAsync(PayBillRequestLogDto payBillRequestLog)
    {
        _logger.LogInformation("QueryAsync request: ======== NOT IMPLEMENTED ====== /n" +
                               payBillRequestLog.ToJson());
        throw new NotImplementedException();
    }

    public async Task<MessageResponseBase> CardGetByBatchAsync(CardRequestLogDto cardRequestLog)
    {
        _logger.LogInformation("CardGetByBatchAsync request: ======== NOT IMPLEMENTED ====== /n" +
                               cardRequestLog.ToJson());
        throw new NotImplementedException();
    }

    public async Task<MessageResponseBase> CheckBalanceAsync(string providerCode, string transCode)
    {
        _logger.LogInformation($"{transCode} Get balance request: " + transCode);
        var responseMessage = new MessageResponseBase();
        var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(providerCode);

        if (providerInfo == null)
            return responseMessage;

        if (!_topupGatewayService.ValidConnector(ProviderConst.MOBIFONE, providerInfo.ProviderCode))
        {
            _logger.LogError($"{providerCode}-{transCode}-MobifoneConnector ProviderConnector not valid");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
            };
        }

        var balanceRequest = new MobifoneRequest()
        {
            Account = providerInfo.Username,
            Password = providerInfo.Password,
            TransType = MobifoneRequest.Type_Balance
        };

        _logger.LogInformation($"{transCode} MOBIFONEConnector Balance object send: " + balanceRequest.ToJson());
        var result = await CallApi(balanceRequest, providerInfo);

        if (result != null)
        {
            _logger.LogInformation($"{transCode} Balance return: {transCode}-{result.ToJson()}");
            if (result.Result.ToString() == "0")
            {
                responseMessage.ResponseCode = ResponseCodeConst.Success;
                responseMessage.ResponseMessage = "Giao dịch thành công";
                responseMessage.Payload = result.Avail;
            }
            else if (new[] { "5000", "501102" }.Contains(result.Result.ToString()))
            {
                //var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.MOBIFONE, result.Result.ToString(), transCode);
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả xử lý.";
            }
            else
            {
                var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.MOBIFONE,
                    result.Result.ToString(), transCode);
                responseMessage.ResponseCode = reResult != null ? reResult.ResponseCode : ResponseCodeConst.Error;
                responseMessage.ResponseMessage = reResult != null ? reResult.ResponseName : result.ResultNamespace;
            }
        }
        else
        {
            _logger.LogInformation($"{transCode} Error send request");
            responseMessage.ResponseMessage = "Lỗi kết nối nhà cung cấp";
        }

        return responseMessage;
    }

    public async Task<MessageResponseBase> DepositAsync(DepositRequestDto request)
    {
        _logger.LogInformation("DepositAsync request: ======== NOT IMPLEMENTED ====== /n" +
                               request.ToJson());
        throw new NotImplementedException();
    }

    public Task<MessageResponseBase> PayBillAsync(PayBillRequestLogDto payBillRequestLog)
    {
        _logger.LogInformation("Get paybill request: ======== NOT IMPLEMENTED ====== /n" +
                               payBillRequestLog.ToJson());
        throw new NotImplementedException();
    }

    #region Private

    private async Task<MobifoneResponse> CallApi(MobifoneRequest request, ProviderInfoDto providerInfo,
        bool isLogin = false)
    {
        MobifoneResponse result = new MobifoneResponse
        {
            Result = 501102
        };
        try
        {
            using (_logger.BeginScope("MOBIFONEConnector Send request to provider"))
            {
                _logger.LogInformation("MOBIFONEConnector request: " + request.ToJson());
                var retryCount = 0;
                // var isRetry = false;
                //var target = "";
                try
                {
                    var scv = new UMarketSC_PortTypeClient();

                    string sessionId = await Login(providerInfo);
                    _logger.LogInformation($"MOBIFONEConnector loginResponse: " + sessionId);
                    if (!string.IsNullOrEmpty(sessionId))
                    {
                        if (request.TransType == MobifoneRequest.Type_Balance)
                        {
                            var balanceRequest = new BalanceRequestType()
                            {
                                type = 2,
                                sessionid = sessionId
                            };
                            BalanceResponseType balanceResponse = await scv.balanceAsync(balanceRequest);
                            result.Result = balanceResponse.result;
                            result.ResultNamespace = balanceResponse.result_namespace;
                            result.Avail = (decimal)balanceResponse.avail;
                            result.Current = (decimal)balanceResponse.current;
                            result.TransId = (int)balanceResponse.transid;
                        }

                        if (request.TransType == MobifoneRequest.Type_Topup)
                        {
                            var topupRequest = new BuyRequestType()
                            {
                                type = 2,
                                sessionid = sessionId,
                                amount = request.Amount,
                                recipient = request.Recipient,
                                reference1 = request.Reference1,
                                reference2 = request.Reference2,
                                target = request.Target,
                            };
                            _logger.LogInformation(
                                $"MOBIFONEConnector-{request.Reference2}-{request.Reference1} retry {retryCount} target {request.Target} Call Api request: " +
                                topupRequest.ToJson());
                            StandardBizResponse buyResponse = await scv.buyAsync(topupRequest);
                            _logger.LogInformation(
                                $"MOBIFONEConnector-{request.Reference2}-{request.Reference1} retry {retryCount} target {request.Target} Call Api response: " +
                                buyResponse.ToJson());
                            if (buyResponse.transid > 0)
                            {
                                result.Result = buyResponse.result;
                                result.ResultNamespace = buyResponse.result_namespace;
                                result.TransId = (int)buyResponse.transid;
                            }
                        } //while ((new[] { "39", "38" }.Contains(result.Result.ToString()) || isRetry) && retryCount < 3) ;//
                    }

                    if (request.TransType == MobifoneRequest.Type_Checktrans)
                    {
                        var checkTransRequest = new CheckUtTransRequestType()
                        {
                            sessionid = sessionId,
                            subcriber = request.Recipient,
                            transid_request = request.Reference2
                        };
                        _logger.LogInformation(
                            $"MOBIFONEConnector checktrans request:{request.Reference2}-{request.Reference1}-{checkTransRequest.ToJson()}");
                        CheckUtTransResponseType checkTransResponse = await scv.check_transAsync(checkTransRequest);
                        _logger.LogInformation(
                            $"MOBIFONEConnector checktrans return:{request.Reference2}-{request.Reference1}-{checkTransResponse.ToJson()}");

                        if (checkTransResponse.result.ToString() == "0" && checkTransResponse.transid > 0)
                        {
                            result.Result = checkTransResponse.result;
                            var detail = checkTransResponse.detail;
                            var checkTransDetail = new ChecktransDetail();
                            if (!string.IsNullOrEmpty(detail))
                            {
                                try
                                {
                                    var details = detail.Split(',');
                                    checkTransDetail.TransType = ChecktransDetail.GetValue(details[0]);
                                    //checkTransDetail.TransDate = Convert.ToDateTime(ChecktransDetail.GetValue(details[1]));
                                    //checkTransDetail.TransDate = DateTime.ParseExact(ChecktransDetail.GetValue(details[1]), new string[] { "MM.dd.yyyy", "MM-dd-yyyy", "MM/dd/yyyy" }, provider, DateTimeStyles.None);
                                    //checkTransDetail.DestMSIDN = ChecktransDetail.GetValue(details[2]);
                                    //checkTransDetail.TransId = ChecktransDetail.GetValue(details[3]);
                                    //checkTransDetail.Amount = Convert.ToDecimal(ChecktransDetail.GetValue(details[4]));
                                    checkTransDetail.Result = ChecktransDetail.GetValue(details[5]);
                                }
                                catch (Exception e)
                                {
                                    _logger.LogError($"MOBIFONEConnector-{request.Reference2} Checktrans Error: ",
                                        e.Message);
                                }

                                result.TransId = (int)checkTransResponse.transid;
                                result.ChecktransDetail = checkTransDetail;
                                result.Detail = checkTransDetail.Result;
                                result.ResultNamespace = checkTransResponse.result_namespace;
                            }
                            else
                            {
                                result.TransId = 0;
                                result.ChecktransDetail = checkTransDetail;
                                result.ResultNamespace = checkTransResponse.result_namespace;
                            }
                        }
                        else
                        {
                            result.TransId = (int)checkTransResponse.transid;
                            result.Result = checkTransResponse.result;
                            result.ResultNamespace = checkTransResponse.result_namespace;
                            result.Detail = checkTransResponse.detail;
                        }
                    }

                    //else
                    //{
                    //    _logger.LogError($"MOBIFONEConnector-{request.Reference2} Login Error: ");
                    //    result = new MobifoneResponse
                    //    {
                    //        Result = 501102, //Tự quy định mã này cho trường hợp timeout.
                    //        ResultNamespace = "Giao dịch đang chờ kết quả xử lý!"
                    //    };
                    //}

                    await scv.CloseAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError($"MOBIFONEConnector-{request.Reference2} Topup exception: " + ex.Message);
                    result = new MobifoneResponse
                    {
                        Result = 501102, //Tự quy định mã này cho trường hợp timeout.
                        ResultNamespace = ex.Message
                    };
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInformation($"{request.Reference2} CallApi Exception : " + ex.Message);
            result = new MobifoneResponse
            {
                Result = 501102,
                ResultNamespace = ex.Message
            };
        }

        return result;
    }

    private async Task<string> GetToken(ProviderInfoDto providerInfo)
    {
        try
        {
            var key = $"PayGate_ProviderToken:Items:{providerInfo.ProviderCode}";
            var tokenCache = await _cacheManager.GetEntity<TokenInfo>(key);
            if (tokenCache != null && !string.IsNullOrEmpty(tokenCache.Token))
            {
                _logger.LogInformation($"GetTokenFromCache: {tokenCache.ToJson()}");
                return tokenCache.Token;
            }

            var token = "";
            using (_logger.BeginScope("Send request to provider"))
            {
                try
                {
                    var scv = new UMarketSC_PortTypeClient();
                    var reponse = await scv.createsessionAsync();
                    _logger.LogInformation($"createsessionAsync reponse: {reponse.ToJson()}");
                    if (reponse != null)
                    {
                        if (reponse.result.ToString() == "0")
                        {
                            token = reponse.sessionid;
                            var obj = new TokenInfo
                            {
                                Token = token,
                                ProviderCode = ProviderConst.MOBIFONE,
                                RequestDate = DateTime.UtcNow
                            };
                            await _cacheManager.AddEntity(key, obj, TimeSpan.FromHours(22));
                        }

                        await scv.CloseAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"GetToken Error: {ex.Message}");
                }
            }


            return token;
        }
        catch (Exception e)
        {
            _logger.LogError($"GetToken error:{e.Message}");
            return null;
        }
    }

    private async Task<string> Login(ProviderInfoDto providerInfo)
    {
        try
        {
            var key = $"PayGate_ProviderToken:Items:{providerInfo.ProviderCode}";
            var scv = new UMarketSC_PortTypeClient();
            var sessionId = await GetToken(providerInfo);
            var pin = Getpin(providerInfo.Username, providerInfo.Password, sessionId);
            var loginRequest = new LoginRequestType()
            {
                initiator = providerInfo.Username,
                pin = pin,
                sessionid = sessionId
            };
            StandardBizResponse loginResponse = await scv.loginAsync(loginRequest);
            _logger.LogInformation($"{providerInfo.ProviderCode} - loginResponse: {loginResponse.ToJson()}");
            if (new[] { "9", "10", "15" }.Contains(loginResponse.result.ToString()))
            {
                var reponse = await scv.createsessionAsync();
                if (reponse.result.ToString() == "0")
                {
                    sessionId = reponse.sessionid;
                    var obj = new TokenInfo
                    {
                        Token = sessionId,
                        ProviderCode = ProviderConst.MOBIFONE,
                        RequestDate = DateTime.UtcNow
                    };
                    await _cacheManager.AddEntity(key, obj, TimeSpan.FromHours(22));
                }

                pin = Getpin(providerInfo.Username, providerInfo.Password, sessionId);
                loginRequest = new LoginRequestType()
                {
                    initiator = providerInfo.Username,
                    pin = pin,
                    sessionid = sessionId
                };
                loginResponse = await scv.loginAsync(loginRequest);
                _logger.LogInformation($"{providerInfo.ProviderCode} - loginResponse again: {loginResponse.ToJson()}");
            }

            await scv.CloseAsync();
            if (loginResponse.result.ToString() == "0" || loginResponse.result.ToString() == "18")
            {
                return sessionId;
            }

            return "";
        }
        catch (Exception e)
        {
            _logger.LogError($"GetToken error:{e.Message}");
            return "";
        }
    }

    public Task<ResponseMessageApi<object>> GetProductInfo(GetProviderProductInfo info)
    {
        throw new NotImplementedException();
    }

    public Task<MessageResponseBase> CheckPrePostPaid(string msisdn, string transCode, string providerCode)
    {
        throw new NotImplementedException();
    }

    private string Getpin(string account, string password, string sessionid)
    {
        var result = account.ToLower() + password;
        result = EncryptDataBySHA(result).ToLower();
        result = sessionid + result;
        result = EncryptDataBySHA(result).ToUpper();
        return result;
    }

    private static string EncryptDataBySHA(string plaintext)
    {
        var plainBytes = Encoding.ASCII.GetBytes(plaintext);
        SHA1 sha = new SHA1CryptoServiceProvider();
        var arrEncrypData = sha.ComputeHash(plainBytes);
        return arrEncrypData.Aggregate("", (current, b) =>
            current +
            Convert.ToString(b, 16).ToUpper(CultureInfo.InvariantCulture).PadLeft(2, '0'));
    }

    #endregion Private
}