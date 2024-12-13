using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Topup.Contracts.Commands.Commons;
using Topup.Contracts.Requests.Commons;
using Topup.Shared;
using Topup.Shared.CacheManager;
using Topup.Shared.ConfigDtos;
using Topup.Shared.Helpers;
using Topup.Shared.Utils;


using Topup.TopupGw.Domains.BusinessServices;
using MassTransit;
using Microsoft.Extensions.Logging;


using Topup.Discovery.Requests.Workers;
using Topup.Gw.Model.Commands;
using Topup.TopupGw.Contacts.Dtos;
using Topup.TopupGw.Contacts.Enums;
using ServiceStack;

namespace Topup.TopupGw.Components.Connectors.Gate;

public class GateConnector : GatewayConnectorBase
{
    private readonly ICacheManager _cacheManager;
    private readonly ILogger<GateConnector> _logger;
    private readonly IBus _bus;
    private readonly GrpcClientHepper _grpcClient;
    private readonly ITopupGatewayService _topupGatewayService;
    private const string funcSendTopup = "sendchar";
    private const string funcCheckTran = "getchar";
    private const string funcStop = "pauschar";

    public GateConnector(ITopupGatewayService topupGatewayService, IBus bus, GrpcClientHepper grpcClient,
        ILogger<GateConnector> logger, ICacheManager cacheManager) : base(topupGatewayService)
    {
        _logger = logger;
        _cacheManager = cacheManager;
        _topupGatewayService = topupGatewayService;
        _grpcClient = grpcClient;
        _bus = bus;
    }

    public override async Task<MessageResponseBase> TopupAsync(TopupRequestLogDto topupRequestLog,
        ProviderInfoDto providerInfo)
    {
        _logger.LogInformation("GateConnector Topup request: " + topupRequestLog.ToJson());
        var responseMessage = new MessageResponseBase();
        try
        {
            if (!TopupGatewayService.ValidConnector(ProviderConst.GATE, providerInfo.ProviderCode))
            {
                _logger.LogError(
                    $"{topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{providerInfo.ProviderCode}-GateConnector ProviderConnector not valid");
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                };
            }

            var telco = topupRequestLog.Vendor switch
            {
                "VTE" => "VIETTEL",
                "VNA" => "VINAPHONE",
                "VMS" => "MOBIFONE",
                _ => ""
            };

            string callurl = providerInfo.PublicKeyFile ?? string.Empty;
            var request = new GateRequest
            {
                UserCode = providerInfo.Username,
                RefCode = topupRequestLog.TransCode,
                Mobile = topupRequestLog.ReceiverInfo,
                Amount = topupRequestLog.TransAmount,
                Cardamous = topupRequestLog.TransAmount.ToString(),
                SimType = 0,
                Telco = telco
            };

            if ((topupRequestLog.ProviderSetTransactionTimeout ?? 0) == 0)
                topupRequestLog.ProviderSetTransactionTimeout = providerInfo.TimeoutProvider;

            request.TimeStop = (topupRequestLog.ProviderSetTransactionTimeout ?? 0) > 0
                ? topupRequestLog.ProviderSetTransactionTimeout
                : providerInfo.TimeoutProvider;

            request.Signature = string.Join("", request.UserCode, request.Telco,
                request.SimType, request.Mobile, request.Amount, request.Cardamous, request.RefCode, callurl,
                providerInfo.Password).Md5();

            responseMessage.TransCodeProvider = topupRequestLog.TransCode;
            var input = new List<KeyValuePair<string, string>>()
            {
                new("usercode", request.UserCode),
                new("telco", request.Telco),
                new("simtype", request.SimType.ToString()),
                new("mobile", request.Mobile),
                new("trantype", "0"),
                new("amount", request.Amount.ToString()),
                new("cardamous", request.Cardamous),
                new("refcode", request.RefCode),
                new("callurl", callurl),
                new("timestop", (request.TimeStop ?? 20).ToString()),
                new("sign", request.Signature)
            };

            string url = providerInfo.ApiUrl + $"/api/v1/{funcSendTopup}";
            var result = await CallApiGate(funcSendTopup, url, input, request.RefCode);
            responseMessage.Exception = result.ResponseMessage;
            responseMessage.ProviderResponseCode = result.ResponseCode.ToString();
            responseMessage.ProviderResponseMessage = result.ResponseMessage;
            if (new[] { 1, 0, 2, 501102 }.Contains(result.ResponseCode))
            {
                _logger.LogInformation($"{topupRequestLog.TransCode} - GATE - Tạo giao dịch thành công");
                _logger.LogInformation(
                    $"{topupRequestLog.TransCode} - GATE - Đợi kết quả xử lý xong sau tối đa : {request.TimeStop} s");
                var startTime = DateTime.Now;
                responseMessage = await ProcessTopupAsync(topupRequestLog, providerInfo, request, result.id_tran,
                    responseMessage);
                var endTime = DateTime.Now;
                _logger.LogInformation(
                    $"{topupRequestLog.TransCode} - đã chờ kết quả trong {(endTime - startTime).TotalSeconds} s ==> {responseMessage.ToJson()}");
            }
            else
            {
                topupRequestLog.ModifiedDate = DateTime.Now;
                topupRequestLog.ResponseInfo = result.ToJson();
                _logger.LogInformation(
                    $"GateConnector Topup fail return:{topupRequestLog.ProviderCode}-{topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{result.ToJson()}");
                var reResult =
                    await TopupGatewayService.GetResponseMassageCacheAsync(ProviderConst.GATE,
                        result.ResponseCode.ToString(), providerInfo.ProviderCode);
                topupRequestLog.Status = TransRequestStatus.Fail;
                responseMessage.ResponseCode = reResult != null ? reResult.ResponseCode : ResponseCodeConst.Error;
                responseMessage.ResponseMessage =
                    reResult != null ? reResult.ResponseName : "Provider error";
                topupRequestLog.ModifiedDate = DateTime.Now;
            }

            _logger.Log(LogLevel.Information, $"{topupRequestLog.TransCode} ==> {responseMessage.ToJson()}");
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

    private async Task<MessageResponseBase> ProcessTopupAsync(TopupRequestLogDto topupRequestLog,
        ProviderInfoDto providerInfo, GateRequest request, string transId, MessageResponseBase responseMessage)
    {
        double timeRecord = 0;
        var key = $"PayGate_TopupRequest:Items:{string.Join("_", topupRequestLog.ProviderCode, request.RefCode)}";
        var begin = DateTime.Now;
        await Task.Delay(TimeSpan.FromSeconds(2));
        string url = providerInfo.ApiUrl + $"/api/v1/{funcCheckTran}";
        request.Signature = string.Join("", request.UserCode, request.RefCode, transId, providerInfo.Password).Md5();

        responseMessage.TransCodeProvider = topupRequestLog.TransCode;
        var input = new List<KeyValuePair<string, string>>()
        {
            new("usercode", request.UserCode),
            new("refcode", request.RefCode),
            new("id_tran", transId),
            new("sign", request.Signature),
        };
        var checkResult = await CallApiGate(funcCheckTran, url, input, topupRequestLog.TransCode);
        var noQuery = 1;
        bool isTopupBonus = false;
        while (checkResult.ResponseCode is 501102 or 1 or 0)
        {
            if (checkResult.ResponseCode is 501102)
            {
                var requestLog = await TopupGatewayService.GetTopupRequestLogAsync(topupRequestLog.TransRef,
                    topupRequestLog.ProviderCode);
                if (requestLog != null && (requestLog.Status == TransRequestStatus.Success ||
                                           requestLog.Status == TransRequestStatus.Fail))
                {
                    _logger.LogInformation("Get TopupLog response : " + requestLog.ToJson());
                    checkResult = new GateReponse
                    {
                        ResponseCode = requestLog.Status == TransRequestStatus.Success ? 2 : 98,
                        amount = Convert.ToInt32(requestLog.TransAmount),
                        amoutran = Convert.ToInt32(requestLog.AmountProvider),
                        ResponseMessage = "Ket qua tu callBack",
                    };
                    break;
                }
            }

            Thread.Sleep(1000);
            var responseCache = await _cacheManager.GetEntity<TopupRequestLogDto>(key);
            if (responseCache != null)
                if (responseCache.Status == TransRequestStatus.Success
                    || responseCache.Status == TransRequestStatus.Fail)
                {
                    checkResult.ResponseCode = responseCache.Status == TransRequestStatus.Success ? 2 : 3;
                    checkResult.refcode = responseCache.TransCode;
                    checkResult.amount = responseCache.TransAmount;
                    checkResult.amoutran = responseCache.AmountProvider;
                    checkResult.ResponseMessage = "Ket qua tu cache callBack_NCC";
                    _logger.LogInformation(request.RefCode +
                                           $"GateConnector Lay_ket_qua_tu cache: {checkResult.ToJson()}");
                }

            if (!new[] { 0, 1 }.Contains(checkResult.ResponseCode))
                break;
            else
            {
                checkResult = await CallApiGate(funcCheckTran, url, input, topupRequestLog.TransCode);
                if (checkResult.ResponseCode is 1 or 0)
                {
                    var extraInfo = providerInfo.ExtraInfo ?? string.Empty;
                    var checkSeconds = DateTime.Now.Subtract(begin).TotalSeconds;
                    double totalSeconds = topupRequestLog.ProviderSetTransactionTimeout ?? providerInfo.Timeout;
                    var arrayValues = TopupGatewayService.ConvertArrayCode(extraInfo.Split('|')[1]);
                    if (extraInfo.StartsWith("1") && arrayValues.Contains(topupRequestLog.TransAmount)
                                                  && checkSeconds >= (topupRequestLog.ProviderSetTransactionTimeout ??
                                                                      providerInfo.TimeoutProvider) &&
                                                  checkSeconds < totalSeconds && !isTopupBonus)
                    {
                        //Topup thưởng
                        var topupBonusReponse = await TopupTransWorker(topupRequestLog, providerInfo.PublicKey);
                        if (topupBonusReponse.ResponseCode == ResponseCodeConst.Success)
                        {
                            topupRequestLog.TransIndex = transId;
                            topupRequestLog.Status = TransRequestStatus.Success;
                            responseMessage.ResponseCode = ResponseCodeConst.Success;
                            responseMessage.ResponseMessage = "Thành công";
                            await TopupGatewayService.TopupRequestLogUpdateAsync(topupRequestLog);
                            await PushMessage(topupRequestLog, providerInfo);
                            return responseMessage;
                        }
                        else
                        {
                            string urlpauschar = providerInfo.ApiUrl + $"/api/v1/{funcStop}";
                            await CallApiGate(funcStop, urlpauschar, input, topupRequestLog.TransCode);
                            Thread.Sleep(1000);
                            checkResult = await CallApiGate(funcCheckTran, url, input, topupRequestLog.TransCode);
                        }

                        isTopupBonus = true;
                    }
                }
            }

            var end = DateTime.Now;
            noQuery++;
            if (end.Subtract(begin).TotalSeconds >
                (topupRequestLog.ProviderSetTransactionTimeout ?? providerInfo.TimeoutProvider))
            {
                if (checkResult.ResponseCode is 1 or 0)
                {
                    string urlpauschar = providerInfo.ApiUrl + $"/api/v1/{funcStop}";
                    await CallApiGate(funcStop, urlpauschar, input, topupRequestLog.TransCode);
                    checkResult = await CallApiGate(funcCheckTran, url, input, topupRequestLog.TransCode);
                }

                if (checkResult.ResponseCode is 501102 or 1 or 0)
                {
                    var timmeQuery = await TopupGatewayService.GetTopupRequestLogAsync(
                        topupRequestLog.TransRef,
                        topupRequestLog.ProviderCode);
                    if (timmeQuery != null && (timmeQuery.Status == TransRequestStatus.Success ||
                                               timmeQuery.Status == TransRequestStatus.Fail))
                    {
                        _logger.Log(LogLevel.Information, "Get TopupLog reponse : " + timmeQuery.ToJson());
                        checkResult = new GateReponse
                        {
                            ResponseCode = responseCache.Status == TransRequestStatus.Success ? 2 : 98,
                            ResponseMessage = "Ket qua tu callBack",
                            amount = responseCache.TransAmount,
                            amoutran = responseCache.AmountProvider,
                            refcode = topupRequestLog.TransCode,
                        };
                    }
                }

                timeRecord = end.Subtract(begin).TotalSeconds;
                break;
            }
        }

        _logger.LogInformation(request.RefCode +
                               $"GateConnector TopupReturn: {topupRequestLog.ProviderCode}-{checkResult.ToJson()}");
        try
        {
            responseMessage.ProviderResponseCode = checkResult.ResponseCode.ToString();
            responseMessage.ProviderResponseMessage = checkResult.ResponseMessage;
            if (checkResult.ResponseCode == 2)
            {
                topupRequestLog.ModifiedDate = DateTime.Now;
                topupRequestLog.ResponseInfo = checkResult.ToJson();
                _logger.LogInformation(
                    $"GateConnector Topup return:{topupRequestLog.ProviderCode}-{topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{checkResult.ToJson()}");
                topupRequestLog.Status = TransRequestStatus.Success;
                responseMessage.ResponseCode = ResponseCodeConst.Success;
                responseMessage.ResponseMessage = "Thành công";
                if (checkResult.amoutran > 0 && checkResult.amoutran < checkResult.amount)
                {
                    //Nếu Topup thiếu từ GATE thì phải -Topup bổ sung
                    //1:Topup 1 phần còn thiếu
                    //2:Topup Full toàn bộ
                    //0:Không làm gì
                    int amountTopup = 0;
                    string checkStatusTopup = providerInfo.ApiPassword ?? string.Empty;
                    if (checkStatusTopup.StartsWith("1"))
                        amountTopup = checkResult.amount - checkResult.amoutran;
                    else if (checkStatusTopup.StartsWith("2"))
                        amountTopup = topupRequestLog.TransAmount;
                    if (amountTopup > 0)
                    {
                        await TopupTransWorker(topupRequestLog, providerInfo.PublicKey, isAdditional: true,
                            amount: amountTopup);
                        topupRequestLog.AmountProvider = checkResult.amoutran;
                    }
                }
            }
            else
            {
                var arrayErrors = TopupGatewayService.ConvertArrayCode(providerInfo.IgnoreCode ?? string.Empty);
                if (arrayErrors.Contains(checkResult.ResponseCode))
                {
                    topupRequestLog.ModifiedDate = DateTime.Now;
                    topupRequestLog.ResponseInfo = checkResult.ToJson();
                    _logger.LogInformation(
                        $"GateConnector Topup return:{topupRequestLog.ProviderCode}-{topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{checkResult.ToJson()}");
                    var reResult = await TopupGatewayService.GetResponseMassageCacheAsync(ProviderConst.GATE,
                        checkResult.ResponseCode.ToString(), providerInfo.ProviderCode);
                    topupRequestLog.Status = TransRequestStatus.Fail;
                    responseMessage.ResponseCode =
                        reResult != null ? reResult.ResponseCode : ResponseCodeConst.Error;
                    responseMessage.ResponseMessage = reResult != null
                        ? reResult.ResponseName
                        : "Provider error";
                }
                else
                {
                    responseMessage.ResponseMessage =
                        "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_TimeOut;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInformation(
                $"GateConnector Topup return:{topupRequestLog.ProviderCode}-{topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{checkResult} Exception: {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
            responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
            responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_TimeOut;
            topupRequestLog.Status = TransRequestStatus.Timeout;
            responseMessage.Exception = ex.Message;
            responseMessage.ProviderResponseCode = "501102";
            responseMessage.ProviderResponseMessage = ex.Message;
        }
        finally
        {
            //Gán để có thông tin checkTran khi cần
            topupRequestLog.TransIndex = transId;
            await TopupGatewayService.TopupRequestLogUpdateAsync(topupRequestLog);
            await _cacheManager.ClearCache(key);   
        }
        return responseMessage;
    }

    public override async Task<MessageResponseBase> TransactionCheckAsync(string providerCode, string transCodeToCheck,
        string transCode, string serviceCode = null, ProviderInfoDto providerInfo = null)
    {
        try
        {
            _logger.LogInformation($"{transCodeToCheck}-GateConnector Check request: " + transCode);
            var responseMessage = new MessageResponseBase();

            if (providerInfo == null)
                providerInfo = await TopupGatewayService.ProviderInfoCacheGetAsync(providerCode);

            if (providerInfo == null ||
                !TopupGatewayService.ValidConnector(ProviderConst.GATE, providerInfo.ProviderCode))
            {
                _logger.LogError($"{transCode}-{providerCode}-GateConnector ProviderConnector not valid");
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult,
                    ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ"
                };
            }

            var getTopup = await TopupGatewayService.GetTopupGateTransCode(transCodeToCheck, serviceCode);
            string transIndex = getTopup != null ? getTopup.TransIndex : "";
            var request = new GateRequest
            {
                UserCode = providerInfo.Username,
                RefCode = transCodeToCheck,
                IdTrans = 1
            };

            request.Signature = string.Join("", request.UserCode, request.RefCode, transIndex, providerInfo.Password)
                .Md5();
            var input = new List<KeyValuePair<string, string>>()
            {
                new("usercode", request.UserCode),
                new("refcode", request.RefCode),
                new("id_tran", transIndex),
                new("sign", request.Signature),
            };
            string url = providerInfo.ApiUrl + $"/api/v1/{funcCheckTran}";
            _logger.LogInformation($"{transCodeToCheck}GateConnector CheckTrans  send: " + request.ToJson());
            var checkResult = await CallApiGate(funcCheckTran, url, input, request.RefCode);
            _logger.LogInformation(
                $"{providerCode}-{transCodeToCheck}GateConnector CheckTopup return: {checkResult.ToJson()}");

            if (checkResult.ResponseCode == 2)
            {
                responseMessage.ResponseCode = ResponseCodeConst.Success;
                responseMessage.ResponseMessage = "Thành công";
            }
            else
            {
                var arrayErrors = TopupGatewayService.ConvertArrayCode(providerInfo.IgnoreCode ?? string.Empty);
                if (arrayErrors.Contains(checkResult.ResponseCode))
                {
                    //var reResult = await TopupGatewayService.GetResponseMassageCacheAsync(ProviderConst.GATE,
                    //checkResult.ResponseCode.ToString(), providerCode);
                    responseMessage.ResponseCode = ResponseCodeConst.Error;
                    responseMessage.ResponseMessage = "Provider error";
                }
                else
                {
                    responseMessage.ResponseMessage =
                        "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_TimeOut;
                }
            }

            responseMessage.ProviderResponseCode = checkResult?.ResponseCode.ToString();
            responseMessage.ProviderResponseMessage = checkResult?.ResponseMessage;
            return responseMessage;
        }
        catch (Exception ex)
        {
            return new MessageResponseBase
            {
                ResponseMessage = ex.Message,
                ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult,
                ProviderResponseCode = "501102",
                ProviderResponseMessage = ex.Message
            };
        }
    }

    public override async Task<MessageResponseBase> CheckBalanceAsync(string providerCode, string transCode)
    {
        _logger.LogInformation("{TransCode} Get balance request", transCode);
        var responseMessage = new MessageResponseBase();
        var providerInfo = await TopupGatewayService.ProviderInfoCacheGetAsync(providerCode);

        if (providerInfo == null)
            return responseMessage;

        if (!TopupGatewayService.ValidConnector(ProviderConst.GATE, providerInfo.ProviderCode))
        {
            _logger.LogError("{ProviderCode}-{TransCode}-GateConnector ProviderConnector not valid", providerCode,
                transCode);
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
            };
        }

        string signature = string.Join("", providerInfo.Username, providerInfo.Password).Md5();
        var input = new List<KeyValuePair<string, string>>()
        {
            new KeyValuePair<string, string>("username", providerInfo.Username),
            new KeyValuePair<string, string>("sign", signature),
        };

        string url = providerInfo.ApiUrl + $"/api/v1/getuseramou";
        _logger.LogInformation("{TransCode} Balance object send: {Data}", transCode);
        var result = await CallApiGate("getuseramou", url, input, transCode);

        if (result != null)
        {
            _logger.LogInformation($"{transCode} Balance return: {transCode}-{result.ToJson()}");
            if (result.ResponseCode == 2)
            {
                responseMessage.ResponseCode = ResponseCodeConst.Success;
                responseMessage.ResponseMessage = "Giao dịch thành công";
                responseMessage.Payload = result.amount.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                responseMessage.ResponseCode = ResponseCodeConst.Error;
                responseMessage.ResponseMessage = result.ResponseMessage;
            }
        }
        else
        {
            _logger.LogInformation("{TransCode} Error send request", transCode);
            responseMessage.ResponseMessage = "Lỗi kết nối nhà cung cấp";
        }

        return responseMessage;
    }

    private async Task<GateReponse> CallApiGate(string func, string url, List<KeyValuePair<string, string>> data,
        string transCode)
    {
        string responseString;
        var exception = string.Empty;
        try
        {
            var client = new HttpClient();
            var responseData = await client.PostAsync(url, new FormUrlEncodedContent(data));
            responseString = await responseData.ReadToEndAsync();
            _logger.LogInformation("Function= {func} Gate callapi response {TransCode}-{ResponseString}", func,
                transCode,
                responseString);
        }
        catch (Exception ex)
        {
            _logger.LogError("Trans exception: {Ex}", ex.Message);
            exception = ex.Message;
            responseString = "TIMEOUT";
        }

        if (!string.IsNullOrEmpty(responseString))
        {
            if (responseString == "TIMEOUT")
                return new GateReponse
                {
                    ResponseCode = 501102,
                    ResponseMessage = exception
                };

            if (url.EndsWith("pauschar"))
                return null;

            var responseMessage = responseString.FromJson<GateReponse>();
            return responseMessage;
        }

        return new GateReponse
        {
            ResponseCode = 501102,
            ResponseMessage = "Send request timeout!"
        };
    }

    private async Task PushMessage(TopupRequestLogDto topupRequestLog, ProviderInfoDto providerInfo)
    {
        try
        {
            string checkStatusTopup = providerInfo.ApiPassword ?? string.Empty;
            var data = new TransGatePushCommand()
            {
                TransCode = topupRequestLog.TransCode,
                CreatedDate = DateTime.Now,
                ServiceCode = topupRequestLog.ServiceCode,
                CategoryCode = topupRequestLog.CategoryCode,
                ProductCode = topupRequestLog.ProductCode,
                TransAmount = topupRequestLog.TransAmount,
                FirstAmount = Convert.ToInt32(providerInfo.PublicKey.Split('|')[1]),
                FirstProvider = providerInfo.PublicKey.Split('|')[0],
                Type = checkStatusTopup,
                Provider = providerInfo.ProviderCode,
                Mobile = topupRequestLog.ReceiverInfo,
                Vender = topupRequestLog.Vendor,
                Status = SaleRequestStatus.WaitForResult,
                ChartId = providerInfo.AlarmTeleChatId,
                CorrelationId = Guid.NewGuid(),
            };
            await _bus.Publish<TransGatePushCommand>(data);
        }
        catch (Exception ex)
        {
            _logger.LogError($"{topupRequestLog.TransCode} PushMessage Exception: {ex}");
        }
    }

    private async Task<MessageResponseBase> TopupTransWorker(TopupRequestLogDto log, string providerCode,
        bool isAdditional = false, int amount = 0)
    {
        var responseMessage = new MessageResponseBase();
        try
        {
            int transAmount = amount;
            var publicKey = providerCode.Split('|');
            if (transAmount == 0)
                transAmount = Convert.ToInt32(publicKey[1]);

            var code = transAmount / 1000;

            var gateProviderInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(publicKey[0]);
            if (gateProviderInfo != null)
            {
                string transCode = isAdditional ? log.TransCode : log.TransCode + ".1";
                var infoAccount = gateProviderInfo;
                var requestDto = new WorkerTopupRequest
                {
                    Amount = transAmount,
                    Channel = Channel.API,
                    AgentType = AgentType.AgentApi,
                    AccountType = SystemAccountType.MasterAgent,
                    CategoryCode = log.CategoryCode,
                    ProductCode = log.CategoryCode + "_" + code.ToString(),
                    PartnerCode = infoAccount.ApiUser,
                    ReceiverInfo = log.ReceiverInfo,
                    RequestIp = string.Empty,
                    ServiceCode = log.ServiceCode,
                    StaffAccount = infoAccount.ApiUser,
                    StaffUser = infoAccount.ApiUser,
                    TransCode = transCode,
                    RequestDate = DateTime.Now,
                    IsCheckReceiverType = false,
                    IsNoneDiscount = false,
                    DefaultReceiverType = "",
                    IsCheckAllowTopupReceiverType = false
                };
                var reponseTopup = await _grpcClient.GetClientCluster(GrpcServiceName.Worker).SendAsync(requestDto);

                _logger.LogInformation(
                    $"{transCode} - {providerCode} TopupTransWorker_Topup: reponse: {reponseTopup.ToJson()}");

                if (!string.IsNullOrEmpty(infoAccount.AlarmTeleChatId) && infoAccount.IsAlarm)
                {
                    try
                    {
                        var publicKTelegram = isAdditional ? infoAccount.PublicKey.Split('|') : providerCode.Split('|');
                        BotMessageType type = reponseTopup.ResponseStatus.ErrorCode == ResponseCodeConst.Success
                            ? BotMessageType.Message
                            : reponseTopup.ResponseStatus.ErrorCode == ResponseCodeConst.Error
                                ? BotMessageType.Error
                                : BotMessageType.Wraning;

                        bool isSend = true;
                        if (publicKTelegram.Length >= 3)
                        {
                            var sKey = publicKTelegram[2].Split('-');
                            if (type == BotMessageType.Message && sKey[0] == "0")
                                isSend = false;
                            else if (type == BotMessageType.Wraning && sKey.Length >= 2 && sKey[1] == "0")
                                isSend = false;
                            else if (type == BotMessageType.Error && sKey.Length >= 3 && sKey[2] == "0")
                                isSend = false;
                        }

                        if (isSend)
                        {
                            await _bus.Publish<SendBotMessageToGroup>(new
                            {
                                MessageType = type,
                                BotType = BotType.Private,
                                ChatId = infoAccount.AlarmTeleChatId,
                                Module = "TopupGate",
                                Title = isAdditional ? "Nạp GD thiếu" : "Nạp check số",
                                Message =
                                    $"Mã GD: {requestDto.TransCode}\n" +
                                    $"Đại lý: {requestDto.PartnerCode}\n" +
                                    $"Sản phẩm {requestDto.ProductCode}\n" +
                                    $"Tài khoản thụ hưởng: {requestDto.ReceiverInfo}\n" +
                                    $"Số tiền nạp: {requestDto.Amount.ToFormat("đ")}\n" +
                                    $"Hình thức nap: {(isAdditional ? "Nạp bù tiền" : "Check số")}\n" +
                                    $"Trạng thái: {reponseTopup.ResponseStatus.ErrorCode}\n" +
                                    $"Nội dung: {reponseTopup.ResponseStatus.Message}",
                                TimeStamp = DateTime.Now,
                                CorrelationId = Guid.NewGuid()
                            });
                        }
                    }
                    catch (Exception botMessage)
                    {
                        _logger.LogError(
                            $"{log.TransRef} -{isAdditional}- {providerCode} TopupTransWorker_Topup_botMessage Exception: {botMessage}");
                    }
                }

                var responseStatus = new MessageResponseBase
                {
                    TransCode = requestDto.TransCode,
                    ResponseCode = reponseTopup.ResponseStatus.ErrorCode,
                    ResponseMessage = reponseTopup.ResponseStatus.Message
                };

                return responseStatus;
            }
            else
            {
                _logger.LogInformation(
                    $"GET: {publicKey[0]} TopupTransWorker_Topup_Profile: {gateProviderInfo.ToJson()}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"{log.TransRef} -{isAdditional}- {providerCode} TopupTransWorker_Topup Exception: {ex}");
        }

        return responseMessage;
    }

    [DataContract]
    internal class GateRequest
    {
        [DataMember(Name = "usercode")] public string UserCode { get; set; }
        [DataMember(Name = "refcode")] public string RefCode { get; set; }
        [DataMember(Name = "mobile")] public string Mobile { get; set; }
        [DataMember(Name = "amount")] public int Amount { get; set; }

        [DataMember(Name = "cardamous")] public string Cardamous { get; set; }

        [DataMember(Name = "telco")] public string Telco { get; set; }
        [DataMember(Name = "simtype")] public int SimType { get; set; }
        [DataMember(Name = "sign")] public string Signature { get; set; }
        [DataMember(Name = "timestop")] public int? TimeStop { get; set; }
        [DataMember(Name = "id_tran")] public int IdTrans { get; set; }
    }

    internal class GateReponse
    {
        [DataMember(Name = "status")] public int ResponseCode { get; set; }


        [DataMember(Name = "message")] public string ResponseMessage { get; set; }

        public string telco { get; set; }

        public string simtype { get; set; }

        public string mobile { get; set; }

        public int amount { get; set; }

        public int amoutran { get; set; }

        public string refcode { get; set; }

        public string id_tran { get; set; }
    }
}