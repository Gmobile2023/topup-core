using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography;
using System.Text;
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
using SocketsHttpHandler = System.Net.Http.SocketsHttpHandler;

namespace Topup.TopupGw.Components.Connectors.PayPoo
{
    public class PayPooConnector : IGatewayConnector
    {
        private const string VIETTEL_SOAP =
            "<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\" xmlns:par=\"https://affapi.viettel.vn/\"><soapenv:Header/><soapenv:Body><par:process><cmd>{0}</cmd><data>{1}</data><signature>{2}</signature></par:process></soapenv:Body></soapenv:Envelope>";

        private readonly ITopupGatewayService _topupGatewayService;

        private readonly ILogger<PayPooConnector> _logger;

        private static Dictionary<string, HttpClient> _lazyClients = new();

        public PayPooConnector(ITopupGatewayService topupGatewayService, ILogger<PayPooConnector> logger)
        {
            _topupGatewayService = topupGatewayService;
            _logger = logger;
        }

        private static HttpClient InitializeHttpClient(string baseAddress)
        {
            var client = new HttpClient(new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(3),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
                SslOptions = new SslClientAuthenticationOptions
                {
                    // Leave certs unvalidated for debugging
                    RemoteCertificateValidationCallback = delegate { return true; },
                }
            })
            {
                BaseAddress = new Uri(baseAddress),
                Timeout = TimeSpan.FromSeconds(150),
            };

            // Perform any initialization here
            client.DefaultRequestHeaders.Add("X-Gravitee-Api-Key", "408e5036-9196-47e9-97fb-b40988eafa68");
            return client;
        }

        public async Task<MessageResponseBase> TopupAsync(TopupRequestLogDto topupRequestLog,
            ProviderInfoDto providerInfo)
        {
            _logger.LogInformation($"{topupRequestLog.TransCode} PayPooConnector topup request: " +
                                   topupRequestLog.ToJson());
            var responseMessage = new MessageResponseBase();
            try
            {
                if (!_topupGatewayService.ValidConnector(ProviderConst.PAYPOO, providerInfo.ProviderCode))
                {
                    _logger.LogError(
                        $"{topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{providerInfo.ProviderCode}-PayPooConnector ProviderConnector not valid");
                    return new MessageResponseBase
                    {
                        ResponseCode = ResponseCodeConst.Error,
                        ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                    };
                }

                string serviceCode =
                    topupRequestLog.ProductCode.StartsWith("VTE_TOPUP") || topupRequestLog.ProductCode == "VTE_BILL"
                        ? "100000"
                        : "000004";
                var encryptedPassword = string.IsNullOrEmpty(providerInfo.ApiPassword)
                    ? Encrypt(providerInfo.Password, providerInfo.PublicKey)
                    : providerInfo.ApiPassword;

                var data = new DataObject
                {
                    OrderId = topupRequestLog.TransCode,
                    Username = providerInfo.Username,
                    Password = encryptedPassword,
                    ServiceCode = serviceCode,
                    Amount = topupRequestLog.TransAmount,
                    ChannelInfo = "https://web.whatsapp.com/1345",
                    BillingCode = topupRequestLog.ReceiverInfo.StartsWith("0")
                        ? topupRequestLog.ReceiverInfo.Substring(1, topupRequestLog.ReceiverInfo.Length - 1)
                        : topupRequestLog.ReceiverInfo,
                };
                responseMessage.TransCodeProvider = topupRequestLog.TransCode;
                var json = data.ToJson();
                var cmd = new PayPooPayRequest
                {
                    Cmd = "PAY_TELECHARGE_VT",
                    Data = data,
                    Signature = Sign(json, providerInfo.PrivateKeyFile)
                };
                var cmdJson = cmd.ToJson();
                var result = await CallApi(providerInfo.ApiUrl, "/ps/telecharge-pay", cmdJson,
                    data.OrderId, data.BillingCode);
                if (result != null)
                {
                    topupRequestLog.ResponseInfo = result.Data.ToJson();
                    topupRequestLog.ModifiedDate = DateTime.Now;
                    responseMessage.ProviderResponseCode = result?.Data.ErrorCode;
                    responseMessage.ProviderResponseMessage = result?.Data.ErrorMsg;

                    //_logger.LogInformation($"{topupRequestLog.ProviderCode}-{topupRequestLog.TransCode} PayPooConnector return: {topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{result.Data.ToJson()}");
                    if (result.Data.ErrorCode == ResponseCodeConst.Error)
                    {
                        topupRequestLog.Status = TransRequestStatus.Success;
                        responseMessage.ResponseCode = ResponseCodeConst.Success;
                        responseMessage.ResponseMessage = "Giao dịch thành công";
                        responseMessage.ProviderResponseTransCode = result.Data.TransId;
                        responseMessage.ReceiverType = result.Data.TppType;
                    }
                    else if (new[] { "32", "232", "233", "05", "605", "K02", "650", "501102" }.Contains(result.Data
                                 .ErrorCode))
                    {
                        // var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.PAYPOO,
                        //     result.Data.ErrorCode, topupRequestLog.TransCode);
                        topupRequestLog.Status = TransRequestStatus.Timeout;
                        responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                        responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả xử lý.";
                    }
                    else
                    {
                        try
                        {
                            string errorCode = result.Data.ErrorCode;
                            var mapError = (result.Data.ErrorMsg ?? string.Empty).Split(':')[0];
                            if (!string.IsNullOrEmpty(mapError) && !string.IsNullOrEmpty(providerInfo.IgnoreCode) &&
                                providerInfo.IgnoreCode.Contains(mapError))
                                errorCode = result.Data.ErrorCode + "_" + mapError;
                            var reResult =
                                await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.PAYPOO, errorCode,
                                    topupRequestLog.TransCode);
                            if (reResult == null && errorCode != result.Data.ErrorCode)
                                reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.PAYPOO,
                                    result.Data.ErrorCode, topupRequestLog.TransCode);

                            topupRequestLog.Status = TransRequestStatus.Fail;
                            responseMessage.ResponseCode =
                                reResult != null ? reResult.ResponseCode : ResponseCodeConst.ResponseCode_ErrorProvider;
                            responseMessage.ResponseMessage =
                                reResult != null ? reResult.ResponseName : "Giao dịch lỗi phía NCC";
                        }
                        catch (Exception exx)
                        {
                            _logger.LogInformation($"{topupRequestLog.TransCode} Error {exx}");
                            responseMessage.ResponseCode = ResponseCodeConst.Error;
                            topupRequestLog.Status = TransRequestStatus.Fail;
                            responseMessage.Exception = exx.Message;
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

        public async Task<MessageResponseBase> TransactionCheckAsync(string providerCode, string transCodeToCheck,
            string transCode, string serviceCode = null, ProviderInfoDto providerInfo = null)
        {
            _logger.LogInformation($"{transCodeToCheck} PayPooConnector check request: " + transCodeToCheck + "|" +
                                   transCode);
            var responseMessage = new MessageResponseBase();
            try
            {
                if (providerInfo == null)
                    providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(providerCode);


                if (providerInfo == null ||
                    !_topupGatewayService.ValidConnector(ProviderConst.PAYPOO, providerInfo.ProviderCode))
                {
                    _logger.LogError($"{transCode}-{providerCode}-PayPooConnector ProviderConnector not valid");
                    return new MessageResponseBase
                    {
                        ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult,
                        ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ"
                    };
                }

                var encryptedPassword = string.IsNullOrEmpty(providerInfo.ApiPassword)
                    ? Encrypt(providerInfo.Password, providerInfo.PublicKey)
                    : providerInfo.ApiPassword;
                var data = new DataObject
                {
                    OrderId = transCode,
                    OriginalOrderId = transCodeToCheck,
                    Username = providerInfo.Username,
                    Password = encryptedPassword,
                    ServiceCode = "100000",
                };
                var json = data.ToJson();

                var cmd = new PayPooPayRequest
                {
                    Cmd = "CHECK_TRANSACTION",
                    Data = data,
                    Signature = Sign(json, providerInfo.PrivateKeyFile)
                };
                var cmdJson = cmd.ToJson();
                //_logger.LogInformation("Trans Check object send: " + Cmd.ToJson());
                var result = await CallApi(providerInfo.ApiUrl, "/ps/check-transaction", cmdJson,
                    data.OrderId, data.BillingCode);
                if (result != null)
                {
                    _logger.LogInformation(
                        $"{providerCode}-{transCodeToCheck} PayPooConnector Check trans return: {transCodeToCheck}-{transCode}-{result.Data.ToJson()}");
                    if (result.Data.ErrorCode == ResponseCodeConst.Error)
                    {
                        if (result.Data.ReferenceCode == ResponseCodeConst.Error)
                        {
                            responseMessage.ResponseCode = ResponseCodeConst.Success;
                            responseMessage.ResponseMessage = "Giao dịch thành công";
                            responseMessage.ProviderResponseTransCode = result.Data.TransId;
                            responseMessage.ReceiverType = result.Data.TppType;
                            if (serviceCode.StartsWith("PIN"))
                            {
                                var cards = await GetPinCodeAsync(providerInfo, providerCode, transCodeToCheck,
                                    transCodeToCheck + DateTime.Now.ToString("HHmmssfff"));
                                if (cards != null && cards.Count > 0)
                                    responseMessage.Payload = cards;
                            }
                        }
                        else if (new[] { "32", "232", "233", "605", "K02", "650" }.Contains(result.Data.ReferenceCode))
                        {
                            // var reResult =
                            //     await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.PAYPOO,
                            //         result.Data.ReferenceCode,
                            //         transCodeToCheck);
                            responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                            responseMessage.ResponseMessage = "Giao dịch chưa có kết quả từ NCC";
                            responseMessage.ProviderResponseCode = result?.Data.ReferenceCode;
                            responseMessage.ProviderResponseMessage = result?.Data.ReferenceMessage;
                        }
                        else
                        {
                            // var reResult =
                            //     await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.PAYPOO,
                            //         result.Data.ReferenceCode,
                            //         transCodeToCheck);
                            responseMessage.ResponseCode = ResponseCodeConst.Error;
                            responseMessage.ResponseMessage = "Giao dịch lỗi phía NCC";
                            //     reResult != null ? reResult.ReponseName : result.Data.ReferenceMessage;
                            // responseMessage.ProviderResponseCode = result?.Data.ReferenceCode;
                            // responseMessage.ProviderResponseMessage = result?.Data.ReferenceMessage;
                        }
                    }
                    else
                    {
                        // var reResult =
                        //     await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.PAYPOO, result.Data.ErrorCode,
                        //         transCodeToCheck);
                        responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                        responseMessage.ResponseMessage =
                            "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                        responseMessage.ProviderResponseCode = result?.Data.ErrorCode;
                        responseMessage.ProviderResponseMessage = result?.Data.ErrorMsg;
                    }
                }
                else
                {
                    _logger.LogInformation($"{transCodeToCheck} Error send request");
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage =
                        "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                }
            }
            catch (Exception e)
            {
                _logger.LogError($"{transCodeToCheck} CheckTrans error {e}");
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                responseMessage.ResponseMessage =
                    "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
            }

            return responseMessage;
        }

        public async Task<NewMessageResponseBase<InvoiceResultDto>> QueryAsync(PayBillRequestLogDto payBillRequestLog)
        {
            _logger.LogInformation($"{payBillRequestLog.TransCode} PayPooConnector Query request: " +
                                   payBillRequestLog.ToJson());
            var responseMessage = new NewMessageResponseBase<InvoiceResultDto>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Truy vấn thông tin không thành công")
            };
            var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(payBillRequestLog.ProviderCode);

            if (providerInfo == null)
                return responseMessage;


            if (!_topupGatewayService.ValidConnector(ProviderConst.PAYPOO, providerInfo.ProviderCode))
            {
                _logger.LogError(
                    $"{payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{providerInfo.ProviderCode}-PayPooConnector ProviderConnector not valid");
                responseMessage.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error,
                    "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ");
                return responseMessage;
            }


            var encryptedPassword = string.IsNullOrEmpty(providerInfo.ApiPassword)
                ? Encrypt(providerInfo.Password, providerInfo.PublicKey)
                : providerInfo.ApiPassword;

            var data = new DataObject
            {
                OrderId = payBillRequestLog.TransCode,
                Username = providerInfo.Username,
                Password = encryptedPassword,
                ServiceCode = "100000",
                BillingCode = payBillRequestLog.ReceiverInfo.StartsWith("0")
                    ? payBillRequestLog.ReceiverInfo.Substring(1, payBillRequestLog.ReceiverInfo.Length - 1)
                    : payBillRequestLog.ReceiverInfo,
            };

            var cmd = new PayPooPayRequest
            {
                Cmd = "GET_TELECHARGE_VT_INFO",
                Data = data,
                Signature = Sign(data.ToJson(), providerInfo.PrivateKeyFile)
            };
            var cmdJson = cmd.ToJson();
            _logger.LogInformation("PayPooConnector send: " + cmdJson);
            var result = await CallApi(providerInfo.ApiUrl, "/ps/telecharge-info", cmdJson,
                data.OrderId, data.BillingCode);

            if (result != null)
            {
                _logger.LogInformation(
                    $"{payBillRequestLog.ProviderCode}-{payBillRequestLog.TransCode} PayPooConnector return: {payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{result.Data.ToJson()}");
                if (result.Data.ErrorCode == ResponseCodeConst.Error)
                {
                    var dto = new InvoiceResultDto()
                    {
                        Amount = result.Data.Amount ?? 0,
                    };
                    responseMessage.ResponseStatus.ErrorCode = ResponseCodeConst.Success;
                    responseMessage.ResponseStatus.Message = result.Data.ErrorMsg;
                    responseMessage.Results = dto;
                }
                else if (new[] { "K82" }.Contains(result.Data.ErrorCode))
                {
                    responseMessage.ResponseStatus.ErrorCode = ResponseCodeConst.Error;
                    responseMessage.ResponseStatus.Message = result.Data.ErrorMsg;
                }
                else if (new[] { "32", "232", "233", "605", "K02", "650", "501102", "05" }.Contains(result.Data
                             .ErrorCode))
                {
                    var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.PAYPOO,
                        result.Data.ErrorCode,
                        payBillRequestLog.TransCode);
                    responseMessage.ResponseStatus.ErrorCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseStatus.Message =
                        reResult != null
                            ? reResult.ResponseName
                            : "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                }
                else
                {
                    var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.PAYPOO,
                        result.Data.ErrorCode,
                        payBillRequestLog.TransCode);
                    responseMessage.ResponseStatus.ErrorCode =
                        reResult != null ? reResult.ResponseCode : ResponseCodeConst.ResponseCode_ErrorProvider;
                    responseMessage.ResponseStatus.Message =
                        reResult != null ? reResult.ResponseName : "Giao dịch lỗi phía NCC";
                }
            }
            else
            {
                _logger.LogInformation($"{payBillRequestLog.TransCode} Error send request");
                responseMessage.ResponseStatus.Message = "Lỗi kết nối nhà cung cấp";
            }

            return responseMessage;
        }

        public async Task<MessageResponseBase> CardGetByBatchAsync(CardRequestLogDto cardRequestLog)
        {
            _logger.LogInformation($"{cardRequestLog.TransCode} PayPooConnector card request: " +
                                   cardRequestLog.ToJson());
            var responseMessage = new MessageResponseBase();
            var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(cardRequestLog.ProviderCode);

            if (providerInfo == null)
                return responseMessage;

            if (!_topupGatewayService.ValidConnector(ProviderConst.PAYPOO, providerInfo.ProviderCode))
            {
                _logger.LogError(
                    $"{cardRequestLog.TransCode}-{cardRequestLog.TransRef}-{providerInfo.ProviderCode}-PayPooConnector ProviderConnector not valid");
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                };
            }


            var encryptedPassword = string.IsNullOrEmpty(providerInfo.ApiPassword)
                ? Encrypt(providerInfo.Password, providerInfo.PublicKey)
                : providerInfo.ApiPassword;

            var data = new DataCardObject
            {
                OrderId = cardRequestLog.TransCode,
                Username = providerInfo.Username,
                Password = encryptedPassword,
                ServiceCode = "PCEBATCH", //"PCEBATCH",
                Amount = (int)cardRequestLog.TransAmount,
                Quantity = cardRequestLog.Quantity,
                PayerMsisdn = providerInfo.ApiUser
            };
            responseMessage.TransCodeProvider = cardRequestLog.TransCode;
            var json = data.ToJson();
            var cmd = new PayPooPayCardRequest
            {
                Cmd = "PAY_PINCODE_VT_BATCH",
                Data = data,
                Signature = Sign(json, providerInfo.PrivateKeyFile)
            };
            var cmdJson = cmd.ToJson();
            _logger.LogInformation($"{cardRequestLog.TransCode} PayPooConnector send: " + cmdJson);
            var result = await CallApi(providerInfo.ApiUrl, "/ps/pincode-batch", cmdJson,
                data.OrderId, "");

            if (result != null)
            {
                _logger.LogInformation(
                    $"Card return: {cardRequestLog.TransCode}-{cardRequestLog.TransRef}-{result.Data.ToJson()}");
                cardRequestLog.ModifiedDate = DateTime.Now;
                cardRequestLog.ResponseInfo = result.Data.ToJson();
                if (result.Data.ErrorCode == ResponseCodeConst.Error)
                {
                    cardRequestLog.Status = TransRequestStatus.Success;
                    responseMessage.ResponseCode = ResponseCodeConst.Success;
                    responseMessage.ResponseMessage = "Giao dịch thành công";
                    responseMessage.ProviderResponseTransCode = result.Data.TransId;
                    try
                    {
                        var cardList = new List<CardRequestResponseDto>();
                        var arraySubs = result.Data.BillDetail.Split("==");
                        string addStr = string.Empty;
                        foreach (var aray in arraySubs)
                        {
                            if (!string.IsNullOrEmpty(aray))
                            {
                                var strGen = Decrypt(aray + "==", providerInfo.PrivateKeyFile);
                                addStr = addStr + strGen;
                            }
                        }

                        var payPooCards = addStr.FromJson<List<PayPooCards>>();
                        foreach (var payPooCard in payPooCards)
                        {
                            cardList.Add(new CardRequestResponseDto
                            {
                                CardType = "VTE",
                                CardValue = payPooCard.Amount,
                                CardCode = payPooCard.Pincode,
                                Serial = payPooCard.Serial,
                                ExpireDate = DateTime.ParseExact(payPooCard.Expire, "yyyy-MM-ddTHH:mm:ss",
                                    CultureInfo.InvariantCulture).ToString("d"),
                                ExpiredDate = DateTime.ParseExact(payPooCard.Expire, "yyyy-MM-ddTHH:mm:ss",
                                    CultureInfo.InvariantCulture)
                            });
                        }

                        responseMessage.Payload = cardList;
                    }
                    catch (Exception e)
                    {
                        _logger.LogError($"{cardRequestLog.TransCode} Error decrypt cards: " + e.Message);
                    }
                }
                else if (new[] { "32", "232", "233", "605", "K02", "650", "501102", "05" }.Contains(result.Data
                             .ErrorCode))
                {
                    // var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.PAYPOO,
                    //     result.Data.ErrorCode,
                    //     cardRequestLog.TransCode);
                    cardRequestLog.Status = TransRequestStatus.Timeout;
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage =
                        "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                }
                else
                {
                    var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.PAYPOO,
                        result.Data.ErrorCode,
                        cardRequestLog.TransCode);
                    cardRequestLog.Status = TransRequestStatus.Fail;
                    responseMessage.ResponseCode = reResult != null ? reResult.ResponseCode : ResponseCodeConst.ResponseCode_ErrorProvider;
                    responseMessage.ResponseMessage = reResult != null ? reResult.ResponseName : "Giao dịch lỗi phía NCC";
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
            _logger.LogInformation($"{transCode} -PayPoo balance request");
            var responseMessage = new MessageResponseBase();
            try
            {
                var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(providerCode);

                if (providerInfo == null)
                    return responseMessage;

                if (!_topupGatewayService.ValidConnector(ProviderConst.PAYPOO, providerInfo.ProviderCode))
                {
                    _logger.LogError($"{providerCode}-{transCode}-PayPooConnector ProviderConnector not valid");
                    return new MessageResponseBase
                    {
                        ResponseCode = ResponseCodeConst.Error,
                        ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                    };
                }


                var encryptedPassword = string.IsNullOrEmpty(providerInfo.ApiPassword)
                    ? Encrypt(providerInfo.Password, providerInfo.PublicKey)
                    : providerInfo.ApiPassword;
                var data = new DataObject
                {
                    OrderId = transCode,
                    Username = providerInfo.Username,
                    ServiceCode = "CP",
                    Password = encryptedPassword,
                    AccountType = "PRE",
                    TransDate = DateTime.Now.ToString("yyyyMMddHHmmss"),
                };

                var json = data.ToJson();
                var cmd = new PayPooPayRequest
                {
                    Cmd = "CHECK_CP",
                    Data = data,
                    Signature = Sign(json, providerInfo.PrivateKeyFile)
                };
                var cmdJson = cmd.ToJson();
                var result = await CallApi(providerInfo.ApiUrl, "/ps/check-cp", cmdJson, data.OrderId,
                    data.BillingCode);
                _logger.LogInformation($"{transCode} PayPoo balance return:{result.ToJson()}");
                if (result != null)
                {
                    responseMessage.ProviderResponseCode = result.Data.ErrorCode;
                    responseMessage.ProviderResponseMessage = result.Data.ErrorMsg;
                    if (result.Data.ErrorCode == ResponseCodeConst.Error)
                    {
                        responseMessage.ResponseCode = ResponseCodeConst.Success;
                        responseMessage.ResponseMessage = "Giao dịch thành công";
                        responseMessage.Payload = result.Data.Amount;
                    }
                    else if (new[] { "32", "232", "233", "605", "K02", "650", "501102", "05" }
                             .Contains(result.Data.ErrorCode))
                    {
                        var reResult =
                            await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.PAYPOO,
                                result.Data.ErrorCode,
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
                            await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.PAYPOO,
                                result.Data.ErrorCode,
                                transCode);
                        responseMessage.ResponseCode =
                            reResult != null ? reResult.ResponseCode : ResponseCodeConst.ResponseCode_ErrorProvider;
                        responseMessage.ResponseMessage =
                            reResult != null ? reResult.ResponseName : "Giao dịch lỗi phía NCC";
                    }
                }
                else
                {
                    _logger.LogInformation($"{transCode} Error send request. Result null");
                    responseMessage.ResponseMessage = "Lỗi kết nối nhà cung cấp";
                }
            }
            catch (Exception e)
            {
                _logger.LogInformation($"{transCode} PayPooConnector checkbalance error:{e}");
                responseMessage.ProviderResponseCode = ResponseCodeConst.ResponseCode_GMB_CODE;
                //responseMessage.ProviderResponseMessage = e.Message;
                responseMessage.ResponseMessage = e.Message;
            }

            return responseMessage;
        }

        public async Task<MessageResponseBase> DepositAsync(DepositRequestDto request)
        {
            _logger.LogInformation("Get deposit request: " + request.TransCode + "|" + request.Amount);
            var responseMessage = new MessageResponseBase();
            var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(request.ProviderCode);

            if (providerInfo == null)
                return responseMessage;

            if (!_topupGatewayService.ValidConnector(ProviderConst.PAYPOO, providerInfo.ProviderCode))
            {
                _logger.LogError(
                    $"{request.TransCode}-{request.Amount}-{providerInfo.ProviderCode}-PayPooConnector ProviderConnector not valid");
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ",
                };
            }


            var encryptedPassword = string.IsNullOrEmpty(providerInfo.ApiPassword)
                ? Encrypt(providerInfo.Password, providerInfo.PublicKey)
                : providerInfo.ApiPassword;

            var data = new DataObject
            {
                AccNo = providerInfo.ExtraInfo.Split('|')[1],
                Amount = (decimal)request.Amount,
                BankCode = providerInfo.ExtraInfo.Split('|')[0],
                OrderId = request.TransCode,
                ServiceCode = "PAYMENT",
                Username = providerInfo.Username,
                Password = encryptedPassword,
            };

            var cmd = new PayPooPayRequest
            {
                Cmd = "PAY_IN_PREPAID",
                Data = data,
                Signature = Sign(data.ToJson(), providerInfo.PrivateKeyFile)
            };

            var cmdJson = cmd.ToJson();
            //_logger.LogInformation("Deposit object send: " + Cmd.ToJson());
            var result = await CallApi(providerInfo.ApiUrl, "/ps/pay-in-prepaid", cmdJson,
                data.OrderId, data.BillingCode);

            if (result != null)
            {
                _logger.LogInformation($"Deposit return: {request.TransCode}-{result.ToJson()}");
                responseMessage.ProviderResponseCode = result.Data.ErrorCode;
                responseMessage.ProviderResponseMessage = result.Data.ErrorMsg;
                if (result.Data.ErrorCode == ResponseCodeConst.Error)
                {
                    responseMessage.ResponseCode = ResponseCodeConst.Success;
                    responseMessage.ResponseMessage = "Giao dịch thành công";
                    responseMessage.Payload = result.Data.Balance;
                }
                else if (new[] { "32", "232", "233", "605", "K02", "650", "501102", "05" }.Contains(result.Data
                             .ErrorCode))
                {
                    var reResult =
                        await _topupGatewayService.GetResponseMassageCacheAsync("VIETTEL", result.Data.ErrorCode,
                            request.TransCode);
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage =
                        reResult != null
                            ? reResult.ResponseName
                            : "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                }
                else
                {
                    var reResult =
                        await _topupGatewayService.GetResponseMassageCacheAsync("VIETTEL", result.Data.ErrorCode,
                            request.TransCode);
                    responseMessage.ResponseCode = ResponseCodeConst.Error;
                    responseMessage.ResponseMessage = reResult != null ? reResult.ResponseName : "Giao dịch lỗi phía NCC";
                }
            }
            else
            {
                _logger.LogInformation("Error send request");
                responseMessage.ResponseMessage = "Lỗi kết nối nhà cung cấp";
            }

            return responseMessage;
        }

        public async Task<MessageResponseBase> PayBillAsync(PayBillRequestLogDto payBillRequestLog)
        {
            _logger.LogInformation($"{payBillRequestLog.TransCode} VimoConnector Paybill request: " +
                                   payBillRequestLog.ToJson());
            var responseMessage = new MessageResponseBase();

            var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(payBillRequestLog.ProviderCode);

            if (providerInfo == null)
                return responseMessage;

            if (!_topupGatewayService.ValidConnector(ProviderConst.PAYPOO, providerInfo.ProviderCode))
            {
                _logger.LogError(
                    $"{payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{providerInfo.ProviderCode}-PayPooConnector ProviderConnector not valid");
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                };
            }

            var providerService =
                providerInfo.ProviderServices.Find(p => p.ProductCode == payBillRequestLog.ProductCode);
            var serviceCode = string.Empty;
            var publisher = string.Empty;
            if (providerService != null)
            {
                serviceCode = providerService.ServiceCode;
            }
            else
            {
                _logger.LogWarning(
                    $"{payBillRequestLog.TransCode} ProviderService with ProductCode [{payBillRequestLog.ProductCode}] is null");
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Giao dịch lỗi. Thông tin sản phẩm nhà cung cấp chưa cấu hình"
                };
            }

            if (!_topupGatewayService.ValidConnector(ProviderConst.PAYPOO, providerInfo.ProviderCode))
            {
                _logger.LogError(
                    $"{payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{providerInfo.ProviderCode}-PayPooConnector ProviderConnector not valid");
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                };
            }

            var encryptedPassword = string.IsNullOrEmpty(providerInfo.ApiPassword)
                ? Encrypt(providerInfo.Password, providerInfo.PublicKey)
                : providerInfo.ApiPassword;

            var data = new DataObject
            {
                OrderId = payBillRequestLog.TransCode,
                Username = providerInfo.Username,
                Password = encryptedPassword,
                ServiceCode = serviceCode,
                Amount = payBillRequestLog.TransAmount,
                ChannelInfo = "https://web.whatsapp.com/1345",
                BillingCode = payBillRequestLog.ReceiverInfo.StartsWith("0")
                    ? payBillRequestLog.ReceiverInfo.Substring(1, payBillRequestLog.ReceiverInfo.Length - 1)
                    : payBillRequestLog.ReceiverInfo,
            };
            responseMessage.TransCodeProvider = payBillRequestLog.TransCode;
            var json = data.ToJson();
            var cmd = new PayPooPayRequest
            {
                Cmd = "PAY_TELECHARGE_VT",
                Data = data,
                Signature = Sign(json, providerInfo.PrivateKeyFile)
            };
            var cmdJson = cmd.ToJson();
            var result = await CallApi(providerInfo.ApiUrl, "/ps/telecharge-pay", cmdJson,
                data.OrderId, data.BillingCode);
            if (result != null)
            {
                payBillRequestLog.ResponseInfo = result.Data.ToJson();
                payBillRequestLog.ModifiedDate = DateTime.Now;
                responseMessage.ProviderResponseCode = result?.Data.ErrorCode;
                responseMessage.ProviderResponseMessage = result?.Data.ErrorMsg;

                //_logger.LogInformation($"{topupRequestLog.ProviderCode}-{topupRequestLog.TransCode} PayPooConnector return: {topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{result.Data.ToJson()}");
                if (result.Data.ErrorCode == ResponseCodeConst.Error)
                {
                    payBillRequestLog.Status = TransRequestStatus.Success;
                    responseMessage.ResponseCode = ResponseCodeConst.Success;
                    responseMessage.ResponseMessage = "Giao dịch thành công";
                    responseMessage.ProviderResponseTransCode = result.Data.TransId;
                    responseMessage.ReceiverType = result.Data.TppType;
                }
                else if (new[] { "32", "232", "233", "05", "605", "K02", "650", "501102" }.Contains(result.Data
                             .ErrorCode))
                {
                    var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.PAYPOO,
                        result.Data.ErrorCode, payBillRequestLog.TransCode);
                    payBillRequestLog.Status = TransRequestStatus.Timeout;
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage =
                        reResult != null ? reResult.ResponseName : "Giao dịch đang chờ kết quả xử lý.";
                }
                else
                {
                    try
                    {
                        string errorCode = result.Data.ErrorCode;
                        var mapError = (result.Data.ErrorMsg ?? string.Empty).Split(':')[0];
                        if (!string.IsNullOrEmpty(mapError) && !string.IsNullOrEmpty(providerInfo.IgnoreCode) &&
                            providerInfo.IgnoreCode.Contains(mapError))
                            errorCode = result.Data.ErrorCode + "_" + mapError;
                        var reResult =
                            await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.PAYPOO, errorCode,
                                payBillRequestLog.TransCode);
                        if (reResult == null && errorCode != result.Data.ErrorCode)
                            reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.PAYPOO,
                                result.Data.ErrorCode, payBillRequestLog.TransCode);

                        payBillRequestLog.Status = TransRequestStatus.Fail;
                        responseMessage.ResponseCode =
                            reResult != null ? reResult.ResponseCode : ResponseCodeConst.ResponseCode_ErrorProvider;
                        responseMessage.ResponseMessage =
                            reResult != null ? reResult.ResponseName : "Giao dịch lỗi phía NCC";
                    }
                    catch (Exception exx)
                    {
                        _logger.LogInformation($"{payBillRequestLog.TransCode} Error {exx}");
                        responseMessage.ResponseCode = ResponseCodeConst.Error;
                        payBillRequestLog.Status = TransRequestStatus.Fail;
                        responseMessage.Exception = exx.Message;
                    }
                }
            }
            else
            {
                _logger.LogInformation($"{payBillRequestLog.TransCode} Error send request");
                responseMessage.ResponseMessage = "Lỗi kết nối nhà cung cấp";
                payBillRequestLog.Status = TransRequestStatus.Fail;
            }

            await _topupGatewayService.PayBillRequestLogUpdateAsync(payBillRequestLog);
            return responseMessage;
        }

        private async Task<List<CardRequestResponseDto>> GetPinCodeAsync(ProviderInfoDto providerInfo,
            string providerCode,
            string transCodeToCheck, string transCode)
        {
            _logger.LogInformation($"{transCodeToCheck} PayPooConnector check request: " + transCodeToCheck + "|" +
                                   transCode);

            List<CardRequestResponseDto> cardList = null;
            var encryptedPassword = string.IsNullOrEmpty(providerInfo.ApiPassword)
                ? Encrypt(providerInfo.Password, providerInfo.PublicKey)
                : providerInfo.ApiPassword;
            var data = new DataObject
            {
                OrderId = transCode,
                OriginalOrderId = transCodeToCheck,
                Username = providerInfo.Username,
                Password = encryptedPassword,
                ServiceCode = "PCEBATCH",
            };
            var json = data.ToJson();

            var cmd = new PayPooPayRequest
            {
                Cmd = "RESEND_PINCODE_VT_BATCH",
                Data = data,
                Signature = Sign(json, providerInfo.PrivateKeyFile)
            };
            var cmdJson = cmd.ToJson();
            //_logger.LogInformation("Trans Check object send: " + Cmd.ToJson());
            var result = await CallApi(providerInfo.ApiUrl, "/ps/resend-pincode-vt-batch",
                cmdJson, data.OrderId, data.BillingCode);
            if (result != null)
            {
                _logger.LogInformation(
                    $"{providerCode}-{transCodeToCheck} PayPooConnector Check trans return: {transCodeToCheck}-{transCode}-{result.Data.ToJson()}");
                if (result.Data.ErrorCode == ResponseCodeConst.Error)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(result.Data.BillDetail))
                        {
                            cardList = new List<CardRequestResponseDto>();
                            var arraySubs = result.Data.BillDetail.Split("==");
                            string addStr = string.Empty;
                            foreach (var aray in arraySubs)
                            {
                                if (!string.IsNullOrEmpty(aray))
                                {
                                    var strGen = Decrypt(aray + "==", providerInfo.PrivateKeyFile);
                                    addStr += strGen;
                                }
                            }

                            var viettelCards = addStr.FromJson<List<PayPooCards>>();
                            foreach (var viettelCard in viettelCards)
                            {
                                cardList.Add(new CardRequestResponseDto
                                {
                                    CardType = "VTE",
                                    CardValue = viettelCard.Amount,
                                    CardCode = viettelCard.Pincode.EncryptTripDes(),
                                    Serial = viettelCard.Serial,
                                    ExpireDate = DateTime.ParseExact(viettelCard.Expire, "yyyy-MM-ddTHH:mm:ss",
                                        CultureInfo.InvariantCulture).ToString("d"),
                                    ExpiredDate = DateTime.ParseExact(viettelCard.Expire, "yyyy-MM-ddTHH:mm:ss",
                                        CultureInfo.InvariantCulture)
                                });
                            }

                            return cardList;
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogError($"{transCodeToCheck} Error decrypt cards: {e.Message}");
                    }
                }
            }

            return cardList;
        }


        #region Private

        private async Task<PayPooPayResponse> CallApi(string baseUrl, string url,
            string data, string orderId, string billingCode)
        {
            var exception = string.Empty;
            string responseString = string.Empty;
            var retryCount = 0;
            var isRetry = false;

            var httpClient = _lazyClients.GetOrAdd(baseUrl, p => InitializeHttpClient(p));
            do
            {
                try
                {
                    using (var content = new StringContent(data, Encoding.UTF8, "application/json"))
                    {
                        _logger.LogInformation($"PayPooConnector send data:{data}");
                        var result = await httpClient.PostAsync(url, content);
                        result.EnsureSuccessStatusCode();
                        responseString = await result.Content.ReadAsStringAsync();
                        _logger.LogInformation($"{orderId}-PayPooConnector response: {responseString}");
                        isRetry = false;
                    }
                }
                catch (TaskCanceledException ex)
                {
                    _logger.LogError(
                        $"TransCode={orderId} - billingCode={billingCode} CallApi_PayPoo_timeout_error: {ex}");
                    responseString = "TIMEOUT";
                    exception = ex.Message;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"TransCode={orderId} - billingCode={billingCode} CallApi_PayPoo_error: {ex}");
                    if (ex.Message.Contains("The SSL connection") ||
                        ex.Message.Contains("Connection refused (affapi.viettel.vn:443)") ||
                        ex.ToString().Contains("Connection reset by peer"))
                    {
                        isRetry = true;
                        retryCount++;
                    }
                    else
                    {
                        responseString = "TIMEOUT";
                    }

                    exception = ex.Message;
                }
            } while ((string.IsNullOrEmpty(responseString) || isRetry) && retryCount < 3);

            if (!string.IsNullOrEmpty(responseString))
            {
                if (responseString == "TIMEOUT")
                {
                    return new PayPooPayResponse()
                    {
                        Data = new DataObject()
                        {
                            ErrorCode = "501102", //Định nghĩa mã lỗi cho trường hợp gọi Service timeout
                            ErrorMsg = $"Send request error! {exception}"
                        }
                    };
                }

                var responseMessage = responseString.FromJson<PayPooPayResponse>();
                return responseMessage;
            }

            return new PayPooPayResponse()
            {
                Data = new DataObject()
                {
                    ErrorCode = "501102",
                    ErrorMsg = $"Send request timeout! - {exception}"
                }
            };
        }


        private string Encrypt(string dataToSign, string key)
        {
            using var rsaViettel = RSA.Create();
            rsaViettel.ImportSubjectPublicKeyInfo(Convert.FromBase64String(key), out _);
            var rsaPublicKey = rsaViettel.ExportParameters(false);
            var passwordByte = Encoding.UTF8.GetBytes(dataToSign);
            var keySize = rsaPublicKey.Modulus.Length;


            var maxLength = keySize - 42;
            var dataLength = passwordByte.Length;
            var iterations = dataLength / maxLength;

            var sb = new StringBuilder();

            for (var i = 0; i <= iterations; ++i)
            {
                var tempBytes = new byte[dataLength - maxLength * i > maxLength
                    ? maxLength
                    : dataLength - maxLength * i];
                Array.Copy(passwordByte, maxLength * i, tempBytes, 0, tempBytes.Length);
                var encryptedBytes = rsaViettel.Encrypt(tempBytes, RSAEncryptionPadding.Pkcs1);
                encryptedBytes = Reverse(encryptedBytes);
                sb.Append(Convert.ToBase64String(encryptedBytes));
            }

            var sEncrypted = sb.ToString();
            sEncrypted = sEncrypted.Replace("\r", "");
            sEncrypted = sEncrypted.Replace("\n", "");
            return sEncrypted;
        }

        private static string Decrypt(string decryptedData, string privateFile)
        {
            var privateKeyText = File.ReadAllText("files/" + privateFile);
            var privateKeyBlocks = privateKeyText.Split("-", StringSplitOptions.RemoveEmptyEntries);
            var privateKeyBytes = Convert.FromBase64String(privateKeyBlocks[1]);

            using var rsa = RSA.Create();

            if (privateKeyBlocks[0] == "BEGIN PRIVATE KEY")
                rsa.ImportPkcs8PrivateKey(privateKeyBytes, out _);
            else if (privateKeyBlocks[0] == "BEGIN RSA PRIVATE KEY") rsa.ImportRSAPrivateKey(privateKeyBytes, out _);

            var rsaPrivateKey = rsa.ExportParameters(true);
            decryptedData = decryptedData.Replace("\r", "");
            decryptedData = decryptedData.Replace("\n", "");
            var passwordByte = Encoding.UTF8.GetBytes(decryptedData);
            var keySize = rsaPrivateKey.Modulus.Length;
            var base64BlockSize = keySize % 3 != 0 ? keySize / 3 * 4 + 4 : keySize / 3 * 4;
            var dataLength = passwordByte.Length;
            var iterations = dataLength / base64BlockSize;

            var listByte = new List<byte>();

            for (var i = 0; i < iterations; ++i)
            {
                var sTemp = decryptedData.Substring(base64BlockSize * i, base64BlockSize * i + base64BlockSize);
                var bTemp = Convert.FromBase64String(sTemp);
                bTemp = Reverse(bTemp);
                var encryptedBytes = rsa.Decrypt(bTemp, RSAEncryptionPadding.Pkcs1);
                listByte.AddRange(encryptedBytes);
            }

            var decrypted = Encoding.UTF8.GetString(listByte.ToArray());

            return decrypted;
        }

        private static byte[] Reverse(byte[] b)
        {
            var left = 0;

            for (var right = b.Length - 1; left < right; --right)
            {
                var temp = b[left];
                b[left] = b[right];
                b[right] = temp;
                ++left;
            }

            return b;
        }

        private string Sign(string dataToSign, string privateFile)
        {
            var privateKeyText = File.ReadAllText("files/" + privateFile);
            var privateKeyBlocks = privateKeyText.Split("-", StringSplitOptions.RemoveEmptyEntries);
            var key = privateKeyBlocks[1].Replace("\r\n", "");
            var privateKeyBytes = Convert.FromBase64String(key);

            using var rsa = RSA.Create();

            if (privateKeyBlocks[0] == "BEGIN PRIVATE KEY")
                rsa.ImportPkcs8PrivateKey(privateKeyBytes, out _);
            else if (privateKeyBlocks[0] == "BEGIN RSA PRIVATE KEY") rsa.ImportRSAPrivateKey(privateKeyBytes, out _);

            var sig = rsa.SignData(
                Encoding.UTF8.GetBytes(dataToSign),
                HashAlgorithmName.SHA1,
                RSASignaturePadding.Pkcs1);
            var signature = Convert.ToBase64String(sig);

            return signature;
        }

        public Task<ResponseMessageApi<object>> GetProductInfo(GetProviderProductInfo info)
        {
            throw new NotImplementedException();
        }

        public Task<MessageResponseBase> CheckPrePostPaid(string msisdn, string transCode, string providerCode)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}