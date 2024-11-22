using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Topup.Shared;
using Topup.Shared.Dtos;
using Topup.Shared.Utils;
using Topup.TopupGw.Contacts.ApiRequests;
using Topup.TopupGw.Contacts.Dtos;
using Topup.TopupGw.Contacts.Enums;
using Topup.TopupGw.Domains.BusinessServices;
using MassTransit;
using Microsoft.Extensions.Logging;
using ServiceStack;

namespace Topup.TopupGw.Components.Connectors.Octa
{
    public class OctaConnector : IGatewayConnector
    {
        private readonly ILogger<OctaConnector> _logger;
        private readonly ITopupGatewayService _topupGatewayService;
        private readonly IBusControl _bus;

        public OctaConnector(ITopupGatewayService topupGatewayService, ILogger<OctaConnector> logger,
            IBusControl bus)
        {
            _topupGatewayService = topupGatewayService;
            _logger = logger;
            ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, errors) => true;
            _bus = bus;
        }

        public async Task<MessageResponseBase> TopupAsync(TopupRequestLogDto topupRequestLog,
            ProviderInfoDto providerInfo)
        {
            using (_logger.BeginScope(topupRequestLog.TransCode))
            {
                _logger.LogInformation($"{topupRequestLog.TransCode} OctaConnector topup request: " +
                                       topupRequestLog.ToJson());
                var responseMessage = new MessageResponseBase();
                try
                {
                    if (!_topupGatewayService.ValidConnector(ProviderConst.OCTA, providerInfo.ProviderCode))
                    {
                        _logger.LogError(
                            $"{topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{providerInfo.ProviderCode}-OctaConnector ProviderConnector not valid");
                        return new MessageResponseBase
                        {
                            ResponseCode = ResponseCodeConst.Error,
                            ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                        };
                    }

                    responseMessage.TransCodeProvider = topupRequestLog.TransCode;
                    var providerService =
                        providerInfo.ProviderServices.Find(p => p.ProductCode == topupRequestLog.ProductCode);
                    var serviceCode = string.Empty;
                    if (providerService != null)
                        serviceCode = providerService.ServiceCode;
                    else
                    {
                        _logger.LogWarning(
                            $"{topupRequestLog.TransCode} ProviderService with ProductCode [{topupRequestLog.ProductCode}] is null");

                        return new MessageResponseBase
                        {
                            ResponseCode = ResponseCodeConst.Error,
                            ResponseMessage = "Giao dịch lỗi. Không tim thấy thông tin kênh"
                        };
                    }

                    var result = await TopupIntMobile(providerInfo, serviceCode, topupRequestLog);
                    if (result != null && result.Response.Code == -5009)
                    {
                        if (serviceCode == "MOBI-PREPAID")
                            serviceCode = "MOBI-POSTPAID";
                        else if (serviceCode == "VINA-PREPAID")
                            serviceCode = "VINA-POSTPAID";
                        else if (serviceCode == "VTEL-PREPAID")
                            serviceCode = "VTEL-POSTPAID";
                        result = await TopupIntMobile(providerInfo, serviceCode, topupRequestLog, retry: 1);
                    }

                    if (result != null)
                    {
                        Console.WriteLine($"{topupRequestLog.TransCode} OctaConnector topup return: " +
                                          result.ToJson());
                        topupRequestLog.ModifiedDate = DateTime.Now;
                        topupRequestLog.ResponseInfo = result.ToJson();

                        _logger.Log(LogLevel.Information,
                            $"{topupRequestLog.ProviderCode}-{topupRequestLog.TransCode} OctaConnector topup return: {topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{result.ToJson()}");
                        responseMessage =
                            await ResponseTopupMessage(result, topupRequestLog.TransCode, topupRequestLog);
                        if (responseMessage.ResponseCode == ResponseCodeConst.Success)
                        {
                            try
                            {
                                responseMessage.ProviderResponseTransCode =
                                    result.Response.Data.Transaction.TransactionID.ToString();
                                var info = result.Response.Data.Transaction.Info.FromJson<OctaTransactionInfo>();
                                if (info != null)
                                {
                                    responseMessage.ReceiverType = info.TppType switch
                                    {
                                        0 => "TT",
                                        1 => "TS",
                                        _ => responseMessage.ReceiverType
                                    };
                                }
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e);
                            }
                        }
                    }
                    else
                    {
                        _logger.LogInformation($"{topupRequestLog.TransCode} Error send request");
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
        }

        public async Task<MessageResponseBase> TransactionCheckAsync(string providerCode, string transCodeToCheck,
            string transCode, string serviceCode = null, ProviderInfoDto providerInfo = null)
        {
            try
            {
                _logger.LogInformation(
                    $"{transCodeToCheck} OctaConnector check request: {transCode}-{transCodeToCheck}");
                var responseMessage = new MessageResponseBase();
                if (providerInfo == null)
                    providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(providerCode);

                if (providerInfo == null ||
                    !_topupGatewayService.ValidConnector(ProviderConst.OCTA, providerInfo.ProviderCode))
                {
                    _logger.LogError(
                        $"{transCode}-{providerCode}-{providerCode}-OctaConnector ProviderConnector not valid");
                    return new MessageResponseBase
                    {
                        ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult,
                        ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ"
                    };
                }


                var client = new JsonHttpClient(providerInfo.ApiUrl)
                {
                    HttpClient = new HttpClient()
                    {
                        Timeout = TimeSpan.FromSeconds(providerInfo.Timeout),
                        BaseAddress = new Uri(providerInfo.ApiUrl)
                    }
                }; //("http://dev.api.zo-ta.com");

                var request = new OctaRequestMessage
                {
                    Request = new Request
                    {
                        Data = new RequestData
                        {
                            ReceiptNumber = transCodeToCheck,
                            RequestDate = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz")
                        }
                    }
                };

                try
                {
                    var sign = Cryptography.HMASHA1Base64(request.ToJson(),
                        Encoding.ASCII.GetBytes(providerInfo.ApiPassword));
                    client.AddHeader("Authorization", $"ECOPAY {providerInfo.ApiUser}:{sign}");
                }
                catch (Exception e)
                {
                    _logger.LogError($"{transCodeToCheck} Error sign data: " + e.Message);
                    return responseMessage;
                }

                _logger.LogInformation($"{transCodeToCheck} OctaConnector check send: " + request.ToJson());
                OctaResponseMessage<OctaCheckTransResponseData> result = null;
                try
                {
                    result = await client.PostAsync<OctaResponseMessage<OctaCheckTransResponseData>>(
                        "/api/v3/GetTransactionInfo",
                        request);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"{transCodeToCheck} OctaConnector check exception: " + ex.Message);
                    result = new OctaResponseMessage<OctaCheckTransResponseData>
                    {
                        Response = new OctaResponse<OctaCheckTransResponseData>
                        {
                            Code = 501102, //Tự quy định mã này cho trường hợp timeout.
                            Message = ex.Message
                        }
                    };
                }

                if (result != null)
                {
                    //responseMessage.ExtraInfo = string.Join("|", result.Response.Code, result.Response.Message);
                    _logger.Log(LogLevel.Information,
                        $"OctaConnector check return: {providerCode}-{transCode}-{transCodeToCheck}-{result.ToJson()}");
                    responseMessage = await ResponseCheckTransMessage(result, transCodeToCheck, serviceCode);
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
            catch (Exception e)
            {
                _logger.LogError($"zota error:{e}");
                return new MessageResponseBase
                {
                    ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ",
                    ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult
                };
            }
        }

        private async Task<MessageResponseBase> ResponseTopupMessage(
            OctaResponseMessage<OctaTransactionResponseData> result,
            string receiptNumber, TopupRequestLogDto topupRequestLog)
        {
            var responseMessage = new MessageResponseBase();

            if (result.Response.Data?.Transaction?.Status == null)
            {
                if (((IList)new[] { -1, -5005, 7, -5024, -5025, -5026, -5027, 501102 }).Contains(result.Response.Code))
                {
                    MessageResponseBase checkResult;
                    var timeToCheck = 0;
                    do
                    {
                        await Task.Delay(TimeSpan.FromSeconds(30));
                        checkResult = await TransactionCheckAsync(topupRequestLog.ProviderCode,
                            receiptNumber,
                            DateTime.Now.ToString("yyMMddHHmmssfff"));
                        timeToCheck++;
                    } while (timeToCheck < 3 &&
                             checkResult.ResponseCode == ResponseCodeConst.ResponseCode_WaitForResult);

                    if (checkResult.ResponseCode != ResponseCodeConst.ResponseCode_WaitForResult)
                    {
                        topupRequestLog.Status = checkResult.ResponseCode == ResponseCodeConst.Success
                            ? TransRequestStatus.Success
                            : TransRequestStatus.Fail;
                        responseMessage = checkResult;
                    }
                    else
                    {
                        topupRequestLog.Status = TransRequestStatus.Timeout;
                        responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                        responseMessage.ResponseMessage =
                            "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                    }
                }
                else if (((IList)new[]
                         {
                             -5, -10, 7, -5001, -5002, -5006, -5007, -5008, -5009, -5010, -5011, -5012, -5013, -5014,
                             -5017,
                             -5019, -5020, -5021, -5022, -5023, -7002
                         }).Contains(result.Response.Code))
                {
                    var reResult = await _topupGatewayService.GetResponseMassageCacheAsync("OCTA",
                        result.Response.Code.ToString(), topupRequestLog.TransCode);
                    topupRequestLog.Status = TransRequestStatus.Fail;
                    responseMessage.ResponseCode = reResult != null ? reResult.ResponseCode : ResponseCodeConst.Error;
                    responseMessage.ResponseMessage = reResult != null ? reResult.ResponseName : result.Response.Message;
                }
                else
                {
                    //var reResult = await _topupGatewayService.GetResponseMassageCacheAsync("OCTA",
                    //result.Response.Code.ToString(), topupRequestLog.TransCode);
                    topupRequestLog.Status = TransRequestStatus.Timeout;
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage = "Chưa có kết quả từ nhà cung cấp";
                }
            }
            else
            {
                if (result.Response.Data.Transaction.Status == 3)
                {
                    topupRequestLog.Status = TransRequestStatus.Success;
                    responseMessage.ResponseCode = ResponseCodeConst.Success;
                    responseMessage.ResponseMessage = "Giao dịch thành công";
                }
                else if (result.Response.Data.Transaction.Status == 1 || result.Response.Data.Transaction.Status == 2)
                {
                    MessageResponseBase checkResult;
                    var timeToCheck = 0;
                    do
                    {
                        await Task.Delay(TimeSpan.FromSeconds(30));
                        checkResult = await TransactionCheckAsync(topupRequestLog.ProviderCode,
                            receiptNumber,
                            DateTime.Now.ToString("yyMMddHHmmssfff"));
                        timeToCheck++;
                    } while (timeToCheck < 3 &&
                             checkResult.ResponseCode == ResponseCodeConst.ResponseCode_WaitForResult);

                    if (checkResult.ResponseCode != ResponseCodeConst.ResponseCode_WaitForResult)
                    {
                        topupRequestLog.Status = checkResult.ResponseCode == ResponseCodeConst.Success
                            ? TransRequestStatus.Success
                            : TransRequestStatus.Fail;
                        responseMessage = checkResult;
                    }
                    else
                    {
                        //var reResult = await _topupGatewayService.GetResponseMassageCacheAsync("OCTA",
                        //result.Response.Data.Transaction.Status.ToString(), topupRequestLog.TransCode);
                        topupRequestLog.Status = TransRequestStatus.Timeout;
                        responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                        responseMessage.ResponseMessage =
                            "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ từ nhà cung cấp";
                    }
                }
                else if (result.Response.Data.Transaction.Status == 0)
                {
                    topupRequestLog.Status = TransRequestStatus.Fail;
                    responseMessage.ResponseCode = ResponseCodeConst.Error;
                    responseMessage.ResponseMessage = "Giao dịch thất bại từ nhà cung cấp";
                }
                else
                {
                    //var reResult = await _topupGatewayService.GetResponseMassageCacheAsync("OCTA",
                    //result.Response.Code.ToString(), topupRequestLog.TransCode);
                    topupRequestLog.Status = TransRequestStatus.Timeout;
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage =
                        "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ từ nhà cung cấp";
                }
            }

            responseMessage.ProviderResponseCode = result?.Response.Code.ToString();
            responseMessage.ProviderResponseMessage = result?.Response.Message;
            return responseMessage;
        }

        private async Task<MessageResponseBase> ResponseCheckTransMessage(
            OctaResponseMessage<OctaCheckTransResponseData> result,
            string transCode, string serviceCode)
        {
            var responseMessage = new MessageResponseBase
            {
                ProviderResponseCode = result?.Response.Code.ToString(),
                ProviderResponseMessage = result?.Response.Message
            };
            if (result.Response.Data?.Status == null)
            {
                if (((IList)new[] { -1, -5005, 7, -5024, -5025, -5026, -5027, 501102 }).Contains(result.Response.Code))
                {
                    //var reResult =
                    //await _topupGatewayService.GetResponseMassageCacheAsync("OCTA", result.Response.Code.ToString(),
                    //transCode);
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage = "Giao dịch chưa có kết quả";
                    return responseMessage;
                }
                else if (((IList)new[]
                         {
                             -5, -10, 7, -5001, -5002, -5006, -5007, -5008, -5009, -5010, -5011, -5012, -5013, -5014,
                             -5017,
                             -5019, -5020, -5021, -5022, -5023, -7002
                         }).Contains(result.Response.Code))
                {
                    //var reResult =
                    //    await _topupGatewayService.GetResponseMassageCacheAsync("OCTA", result.Response.Code.ToString(),
                    //        transCode);
                    responseMessage.ResponseCode = ResponseCodeConst.Error;
                    responseMessage.ResponseMessage = result.Response.Message;
                    return responseMessage;
                }
                else
                {
                    // var reResult =
                    //     await _topupGatewayService.GetResponseMassageCacheAsync("OCTA", result.Response.Code.ToString(),
                    //         transCode);
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage = "Chưa có kết quả từ nhà cung cấp";
                    return responseMessage;
                }
            }
            else
            {
                if (result.Response.Data.Status == 3)
                {
                    responseMessage.ResponseCode = ResponseCodeConst.Success;
                    responseMessage.ResponseMessage = "Giao dịch thành công";
                    responseMessage.Payload = result.Response.Data.Accepted;
                    try
                    {
                        if (serviceCode.StartsWith("PIN"))
                        {
                            var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync("OCTA");
                            var listCards = result.Response.Data.Info.FromJson<List<OctaCardResponse>>();
                            var cardList = listCards.Select(cardText => new CardRequestResponseDto
                                {
                                    CardType = cardText.Provider,
                                    CardValue = cardText.Price.ToString("0"),
                                    CardCode = Cryptography.TripleDesDecrypt(cardText.Code, providerInfo.ApiPassword)
                                        .EncryptTripDes(),
                                    Serial = cardText.Serial,
                                    ExpiredDate = DateTime.ParseExact(cardText.ExpriedDate, "yyyy-MM-dd",
                                        CultureInfo.InvariantCulture), //GetExpireDate(cardText.ExpriedDate),
                                    ExpireDate = DateTime.ParseExact(cardText.ExpriedDate, "yyyy-MM-dd",
                                        CultureInfo.InvariantCulture).ToString("dd/MM/yyyy")
                                })
                                .ToList();

                            responseMessage.Payload = cardList;
                        }
                        else
                        {
                            responseMessage.ProviderResponseTransCode =
                                result.Response.Data.TransactionID.ToString();
                            var info = result.Response.Data.Info.FromJson<OctaTransactionInfo>();
                            if (info != null)
                            {
                                responseMessage.ReceiverType = info.TppType switch
                                {
                                    0 => "TT",
                                    1 => "TS",
                                    _ => responseMessage.ReceiverType
                                };
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }

                    return responseMessage;
                }
                else if (((IList)new[] { 1, 2 }).Contains(result.Response.Data.Status))
                {
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage =
                        "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ từ nhà cung cấp";
                    return responseMessage;
                }
                else if (result.Response.Data.Status == 0)
                {
                    responseMessage.ResponseCode = ResponseCodeConst.Error;
                    responseMessage.ResponseMessage = "Giao dịch không thành công từ nhà cung cấp";
                    responseMessage.Payload = result.Response.Data.Accepted;
                    return responseMessage;
                }
                else
                {
                    // var reResult = await _topupGatewayService.GetResponseMassageCacheAsync("OCTA",
                    //     result.Response.Data.Status.ToString(), transCode);
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage =
                        "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ từ nhà cung cấp";
                    return responseMessage;
                }
            }
        }


        public async Task<NewMessageResponseBase<InvoiceResultDto>> QueryAsync(PayBillRequestLogDto payBillRequestLog)
        {
            return await Task.FromResult(new NewMessageResponseBase<InvoiceResultDto>()
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Nhà cung cấp không hỗ trợ truy vấn")
            });
        }

        public async Task<MessageResponseBase> CardGetByBatchAsync(CardRequestLogDto cardRequestLog)
        {
            _logger.Log(LogLevel.Information,
                $"{cardRequestLog.TransCode} Get card request: " + cardRequestLog.ToJson());
            var responseMessage = new MessageResponseBase();

            var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(cardRequestLog.ProviderCode);

            if (providerInfo == null)
                return responseMessage;

            if (!_topupGatewayService.ValidConnector(ProviderConst.OCTA, providerInfo.ProviderCode))
            {
                _logger.LogError(
                    $"{cardRequestLog.TransCode}-{cardRequestLog.TransRef}-{providerInfo.ProviderCode}-OctaConnector ProviderConnector not valid");
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                };
            }

            responseMessage.TransCodeProvider = cardRequestLog.TransCode;
            var providerService =
                providerInfo.ProviderServices.Find(p => p.ProductCode == cardRequestLog.ProductCode);
            var serviceCode = string.Empty;
            if (providerService != null)
                serviceCode = providerService.ServiceCode;
            else
            {
                _logger.LogWarning(
                    $"{cardRequestLog.TransCode} ProviderService with ProductCode [{cardRequestLog.ProductCode}] is null");
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Giao dịch lỗi. Không tìm thấy thông tin nhà cung cấp"
                };
            }

            var client = new JsonHttpClient(providerInfo.ApiUrl)
            {
                HttpClient = new HttpClient()
                {
                    Timeout = TimeSpan.FromSeconds(providerInfo.Timeout),
                    BaseAddress = new Uri(providerInfo.ApiUrl)
                }
            }; //("http://dev.api.zo-ta.com");
            var request = new OctaRequestMessage
            {
                Request = new Request
                {
                    Data = new RequestData
                    {
                        ReceiptNumber = cardRequestLog.TransCode,
                        RequestDate = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                        Price = (int)cardRequestLog.TransAmount,
                        ServiceCode = serviceCode,
                        Amount = cardRequestLog.Quantity
                    }
                }
            };

            try
            {
                var sign = Cryptography.HMASHA1Base64(request.ToJson(),
                    Encoding.ASCII.GetBytes(providerInfo.ApiPassword));
                client.AddHeader("Authorization", $"ECOPAY {providerInfo.ApiUser}:{sign}");
            }
            catch (Exception e)
            {
                _logger.LogError($"{cardRequestLog.TransCode} Error sign data: " + e.Message);
                cardRequestLog.Status = TransRequestStatus.Fail;
                await _topupGatewayService.CardRequestLogUpdateAsync(cardRequestLog);
            }

            _logger.LogInformation($"{cardRequestLog.TransCode} Card object send: " + request.ToJson());

            OctaResponseMessage<OctaTransactionResponseData> result;
            try
            {
                //ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, errors) => true;
                result = await client.PostAsync<OctaResponseMessage<OctaTransactionResponseData>>("/api/v3/PayCode",
                    request);
            }
            catch (Exception ex)
            {
                _logger.LogError($"{cardRequestLog.TransCode} Get Cards exception: " + ex.Message);
                result = new OctaResponseMessage<OctaTransactionResponseData>
                {
                    Response = new OctaResponse<OctaTransactionResponseData>
                    {
                        Code = 501102, //Tự quy định mã này cho trường hợp timeout.
                        Message = ex.Message
                    }
                };
            }

            if (result != null)
            {
                cardRequestLog.ModifiedDate = DateTime.Now;
                cardRequestLog.ResponseInfo = result.ToJson();
                _logger.Log(LogLevel.Information,
                    $"Card return: {cardRequestLog.TransCode}-{cardRequestLog.TransRef}-{result.ToJson()}");
                responseMessage.ProviderResponseCode = result?.Response.Code.ToString();
                responseMessage.ProviderResponseMessage = result?.Response.Message;
                if (result.Response.Data == null || result.Response.Data.Transaction == null)
                {
                    if (((IList)new[] { -1, -5005, 7, -5024, -5025, -5026, -5027, 501102 }).Contains(
                            result.Response.Code))
                    {
                        MessageResponseBase checkResult;
                        var timeToCheck = 0;
                        do
                        {
                            await Task.Delay(TimeSpan.FromSeconds(30));
                            checkResult = await TransactionCheckAsync(cardRequestLog.ProviderCode,
                                request.Request.Data.ReceiptNumber,
                                DateTime.Now.ToString("yyMMddHHmmssfff"));
                            timeToCheck++;
                        } while (timeToCheck < 3 &&
                                 checkResult.ResponseCode == ResponseCodeConst.ResponseCode_WaitForResult);

                        if (checkResult.ResponseCode != ResponseCodeConst.ResponseCode_WaitForResult)
                        {
                            cardRequestLog.Status = checkResult.ResponseCode == ResponseCodeConst.Success
                                ? TransRequestStatus.Success
                                : TransRequestStatus.Fail;
                            responseMessage = checkResult;
                        }
                        else
                        {
                            cardRequestLog.Status = TransRequestStatus.Timeout;
                            responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                            responseMessage.ResponseMessage =
                                "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                        }
                    }
                    else if (((IList)new[]
                             {
                                 -5, -10, 7, -5001, -5002, -5006, -5007, -5008, -5009, -5010, -5011, -5012, -5013,
                                 -5014, -5017,
                                 -5019, -5020, -5021, -5022, -5023, -7002
                             }).Contains(result.Response.Code))
                    {
                        var reResult = await _topupGatewayService.GetResponseMassageCacheAsync("OCTA",
                            result.Response.Code.ToString(), cardRequestLog.TransCode);
                        cardRequestLog.Status = TransRequestStatus.Fail;
                        responseMessage.ResponseCode =
                            reResult != null ? reResult.ResponseCode : ResponseCodeConst.Error;
                        responseMessage.ResponseMessage =
                            reResult != null ? reResult.ResponseName : result.Response.Message;
                    }
                    else
                    {
                        var reResult = await _topupGatewayService.GetResponseMassageCacheAsync("OCTA",
                            result.Response.Code.ToString(), cardRequestLog.TransCode);
                        cardRequestLog.Status = TransRequestStatus.Timeout;
                        responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                        responseMessage.ResponseMessage =
                            reResult != null ? reResult.ResponseName : "Chưa có kết quả từ nhà cung cấp";
                    }
                }
                else
                {
                    if (result.Response.Data.Transaction.Status == 3)
                    {
                        responseMessage.ResponseCode = ResponseCodeConst.Success;
                        responseMessage.ResponseMessage = "Giao dịch thành công";
                        cardRequestLog.Status = TransRequestStatus.Success;
                        responseMessage.ProviderResponseTransCode =
                            result.Response.Data.Transaction.TransactionID.ToString();
                        try
                        {
                            var listCards = result.Response.Data.Transaction.Info.FromJson<List<OctaCardResponse>>();

                            var cardList = listCards.Select(cardText => new CardRequestResponseDto
                                {
                                    CardType = cardText.Provider,
                                    CardValue = cardText.Price.ToString("0"),
                                    CardCode = Cryptography.TripleDesDecrypt(cardText.Code, providerInfo.ApiPassword),
                                    Serial = cardText.Serial,
                                    ExpiredDate = DateTime.ParseExact(cardText.ExpriedDate, "yyyy-MM-dd",
                                        CultureInfo.InvariantCulture), //GetExpireDate(cardText.ExpriedDate),
                                    ExpireDate = DateTime
                                        .ParseExact(cardText.ExpriedDate, "yyyy-MM-dd", CultureInfo.InvariantCulture)
                                        .ToString("dd/MM/yyyy")
                                })
                                .ToList();

                            responseMessage.Payload = cardList;
                        }
                        catch (Exception e)
                        {
                            _logger.LogError($"{cardRequestLog.TransCode} Error parsing cards: " + e.Message);
                        }
                    }
                    else if (result.Response.Data.Transaction.Status == 1 ||
                             result.Response.Data.Transaction.Status == 2)
                    {
                        MessageResponseBase checkResult;
                        var timeToCheck = 0;
                        do
                        {
                            await Task.Delay(TimeSpan.FromSeconds(30));
                            checkResult = await TransactionCheckAsync(cardRequestLog.ProviderCode,
                                request.Request.Data.ReceiptNumber,
                                DateTime.Now.ToString("yyMMddHHmmssfff"));
                            timeToCheck++;
                        } while (timeToCheck < 3 &&
                                 checkResult.ResponseCode == ResponseCodeConst.ResponseCode_WaitForResult);

                        if (checkResult.ResponseCode != ResponseCodeConst.ResponseCode_WaitForResult)
                        {
                            cardRequestLog.Status = checkResult.ResponseCode == ResponseCodeConst.Success
                                ? TransRequestStatus.Success
                                : TransRequestStatus.Fail;
                            responseMessage = checkResult;
                        }
                        else
                        {
                            var reResult = await _topupGatewayService.GetResponseMassageCacheAsync("OCTA",
                                result.Response.Data.Transaction.Status.ToString(), cardRequestLog.TransCode);
                            cardRequestLog.Status = TransRequestStatus.Timeout;
                            responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                            responseMessage.ResponseMessage = request != null
                                ? reResult.ResponseName
                                : "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ từ nhà cung cấp.";
                        }
                    }
                    else if (result.Response.Data.Transaction.Status == 0)
                    {
                        cardRequestLog.Status = TransRequestStatus.Fail;
                        responseMessage.ResponseCode = ResponseCodeConst.Error;
                        responseMessage.ResponseMessage = "Giao dịch thất bại từ nhà cung cấp.";
                    }
                    else
                    {
                        cardRequestLog.Status = TransRequestStatus.Timeout;
                        responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                        responseMessage.ResponseMessage =
                            "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ từ nhà cung cấp.";
                    }
                }
            }
            else
            {
                _logger.LogInformation($"{cardRequestLog.TransCode} Error send request");
                responseMessage.ResponseMessage = "Lỗi kết nối nhà cung cấp";
                cardRequestLog.Status = TransRequestStatus.Fail;
            }

            await _topupGatewayService.CardRequestLogUpdateAsync(cardRequestLog);
            return responseMessage;
        }

        public async Task<MessageResponseBase> CheckBalanceAsync(string providerCode, string transCode)
        {
            _logger.Log(LogLevel.Information, $"{transCode} Get balance request: " + transCode);
            var responseMessage = new MessageResponseBase();

            var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(providerCode);

            if (providerInfo == null)
                return responseMessage;

            if (!_topupGatewayService.ValidConnector(ProviderConst.OCTA, providerInfo.ProviderCode))
            {
                _logger.LogError(
                    $"{providerCode}-{transCode}-{providerCode}-OctaConnector ProviderConnector not valid");
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                };
            }


            var client = new JsonHttpClient(providerInfo.ApiUrl)
            {
                HttpClient = new HttpClient()
                {
                    Timeout = TimeSpan.FromSeconds(providerInfo.Timeout),
                    BaseAddress = new Uri(providerInfo.ApiUrl)
                }
            }; //("http://dev.api.zo-ta.com");

            var request = new OctaRequestMessage
            {
                Request = new Request
                {
                    RequestDate = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                }
            };
            try
            {
                var sign = Cryptography.HMASHA1Base64(request.ToJson(),
                    Encoding.ASCII.GetBytes(providerInfo.ApiPassword));
                client.AddHeader("Authorization", $"ECOPAY {providerInfo.ApiUser}:{sign}");
            }
            catch (Exception e)
            {
                _logger.LogError($"{transCode} Error sign data: " + e.Message);
            }

            _logger.LogInformation($"{transCode} Balance object send: " + request.ToJson());

            OctaResponseMessage<OctaTransactionResponseData> result = null;
            try
            {
                //ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, errors) => true;
                result = await client.PostAsync<OctaResponseMessage<OctaTransactionResponseData>>(
                    "/api/v3/GetAgentInfo", request);
            }
            catch (Exception ex)
            {
                _logger.LogError($"{transCode} Balance exception: " + ex.Message);
                result = new OctaResponseMessage<OctaTransactionResponseData>
                {
                    Response = new OctaResponse<OctaTransactionResponseData>
                    {
                        Code = 501102, //Tự quy định mã này cho trường hợp timeout.
                        Message = ex.Message
                    }
                };
            }

            if (result != null)
            {
                _logger.Log(LogLevel.Information, $"{transCode} Balance return: {transCode}-{result.ToJson()}");
                if (result.Response.Code == 0)
                {
                    responseMessage.ResponseCode = ResponseCodeConst.Success;
                    responseMessage.ResponseMessage = "Giao dịch thành công";
                    responseMessage.Payload = Convert.ToDouble(result.Response.Data.Balance);
                }
                else if (((IList)new[] { -1, -5005, 7, 501102 }).Contains(result.Response.Code))
                {
                    var reResult =
                        await _topupGatewayService.GetResponseMassageCacheAsync("OCTA", result.Response.Code.ToString(),
                            transCode);
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage =
                        reResult != null
                            ? reResult.ResponseName
                            : "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                }
                else
                {
                    var reResult =
                        await _topupGatewayService.GetResponseMassageCacheAsync("OCTA", result.Response.Code.ToString(),
                            transCode);
                    responseMessage.ResponseCode = reResult != null ? reResult.ResponseCode : ResponseCodeConst.Error;
                    responseMessage.ResponseMessage = reResult != null ? reResult.ResponseName : result.Response.Message;
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
            return await Task.FromResult(new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Nhà cung cấp không hỗ trợ chức năng này"
            });
        }

        public async Task<MessageResponseBase> PayBillAsync(PayBillRequestLogDto payBillRequestLog)
        {
            return await Task.FromResult(new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Nhà cung cấp không hỗ trợ chức năng này"
            });
        }


        private async Task<OctaResponseMessage<OctaTransactionResponseData>> TopupIntMobile(
            ProviderInfoDto providerInfo, string serviceCode,
            TopupRequestLogDto topupRequestLog, int retry = 0)
        {
            var client = new JsonHttpClient(providerInfo.ApiUrl)
            {
                HttpClient = new HttpClient()
                {
                    Timeout = TimeSpan.FromSeconds(providerInfo.Timeout),
                    BaseAddress = new Uri(providerInfo.ApiUrl)
                }
            };

            string transCode = topupRequestLog.TransCode;
            // if (retry == 1 && serviceCode.Contains("POSTPAID"))
            //     transCode = transCode + "_S";

            var request = new OctaRequestMessage
            {
                Request = new Request
                {
                    Data = new RequestData
                    {
                        PhoneNumber = topupRequestLog.ReceiverInfo,
                        ReceiptNumber = transCode,
                        RequestDate = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                        Price = topupRequestLog.TransAmount,
                        ServiceCode = serviceCode
                    }
                }
            };
            try
            {
                var sign =
                    $"ECOPAY {providerInfo.ApiUser}:{Cryptography.HMASHA1Base64(request.ToJson(), Encoding.ASCII.GetBytes(providerInfo.ApiPassword))}";
                client.AddHeader("Authorization", sign);
            }
            catch (Exception e)
            {
                _logger.LogError(
                    $"TransCode= {topupRequestLog.TransCode}|TransCodeConvert= {transCode}|Error sign data: " +
                    e.Message);
                topupRequestLog.Status = TransRequestStatus.Fail;
                await _topupGatewayService.TopupRequestLogUpdateAsync(topupRequestLog);
                return new OctaResponseMessage<OctaTransactionResponseData>()
                {
                    Response = new OctaResponse<OctaTransactionResponseData>()
                    {
                        Code = -1,
                        Message = "Lỗi",
                    },
                };
            }

            //  OctaResponseMessage<OctaTransactionResponseData> result;
            using (_logger.BeginScope("Send request to provider"))
            {
                _logger.LogInformation(
                    $"TransCode= {topupRequestLog.TransCode} OctaConnector topup send:TransCodeConvert= {transCode}|" +
                    request.ToJson());
                //The operation has timed out.
                try
                {
                    //ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, errors) => true;
                    var result1 = await client.PostAsync<string>("/api/v3/Topup", request);
                    return result1.FromJson<OctaResponseMessage<OctaTransactionResponseData>>();
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        $"TransCode= {topupRequestLog.TransCode} OctaConnector exception: TransCodeConvert= {transCode}|" +
                        ex.Message);
                    return new OctaResponseMessage<OctaTransactionResponseData>
                    {
                        Response = new OctaResponse<OctaTransactionResponseData>
                        {
                            Code = 501102, //Tự quy định mã này cho trường hợp timeout.
                            Message = ex.Message
                        }
                    };
                }
            }
        }

        // private DateTime GetExpireDate(string expireDate)
        // {
        //     try
        //     {
        //         string[] formats = { "yyyy/MM/dd" };
        //         if (DateTime.TryParseExact(expireDate, formats, CultureInfo.InvariantCulture,
        //                 DateTimeStyles.None,
        //                 out _))
        //         {
        //             return DateTime.ParseExact(expireDate, formats,
        //                 CultureInfo.InvariantCulture,
        //                 DateTimeStyles.None);
        //         }
        //
        //         return new DateTime(DateTime.Now.AddYears(2).Year, 12, 31);
        //     }
        //     catch (Exception e)
        //     {
        //         _logger.LogError($"{expireDate} getExpireDate Error convert_date : " + e.Message);
        //         return new DateTime(DateTime.Now.AddYears(2).Year, 12, 31);
        //     }
        // }

        public Task<ResponseMessageApi<object>> GetProductInfo(GetProviderProductInfo info)
        {
            throw new NotImplementedException();
        }

        public Task<MessageResponseBase> CheckPrePostPaid(string msisdn, string transCode, string providerCode)
        {
            throw new NotImplementedException();
        }
    }
}