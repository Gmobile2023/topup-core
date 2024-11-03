using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using GMB.Topup.Shared;
using GMB.Topup.Shared.CacheManager;
using GMB.Topup.Shared.Dtos;
using GMB.Topup.Shared.Utils;
using GMB.Topup.TopupGw.Contacts.ApiRequests;
using GMB.Topup.TopupGw.Contacts.Dtos;
using GMB.Topup.TopupGw.Contacts.Enums;
using GMB.Topup.TopupGw.Domains.BusinessServices;
using Microsoft.Extensions.Logging;
using ServiceStack;

namespace GMB.Topup.TopupGw.Components.Connectors.Imedia
{
    public class ImediaConnector : IGatewayConnector
    {
        private readonly ILogger<ImediaConnector> _logger;
        private readonly ITopupGatewayService _topupGatewayService;
        private readonly ICacheManager _cacheManager;

        public ImediaConnector(ITopupGatewayService topupGatewayService, ILogger<ImediaConnector> logger,
            ICacheManager cacheManager)
        {
            _topupGatewayService = topupGatewayService;
            _logger = logger;
            _cacheManager = cacheManager;
        }

        public async Task<MessageResponseBase> TopupAsync(TopupRequestLogDto topupRequestLog,
            ProviderInfoDto providerInfo)
        {
            var responseMessage = new MessageResponseBase();
            try
            {
                using (_logger.BeginScope(topupRequestLog.TransCode))
                {
                    if (!_topupGatewayService.ValidConnector(ProviderConst.IMEDIA, providerInfo.ProviderCode))
                    {
                        return new MessageResponseBase
                        {
                            ResponseCode = ResponseCodeConst.Error,
                            ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                        };
                    }

                    var request = new ImediaRequest
                    {
                        username = providerInfo.Username,
                        merchantPass = providerInfo.Password,
                        requestID = topupRequestLog.TransCode,
                        accountType = "0",
                        topupAmount = topupRequestLog.TransAmount,
                        targetAccount = topupRequestLog.ReceiverInfo,
                        providerCode = topupRequestLog.Vendor,
                        operation = 1200,
                        keyBirthdayTime = "",
                    };

                    if (topupRequestLog.ServiceCode != "TOPUP_DATA")
                    {
                        if (request.providerCode == "VTE")
                            request.providerCode = "Viettel";
                        else if (request.providerCode == "VNA")
                            request.providerCode = "Vinaphone";
                        else if (request.providerCode == "GMOBILE")
                            request.providerCode = "Beeline";
                        else if (request.providerCode == "VNM")
                            request.providerCode = "VNmobile";
                        else if (request.providerCode == "VMS")
                            request.providerCode = "Mobifone";
                        else if (request.providerCode is "WT" or "Wintel")
                            request.providerCode = "Reddi";
                    }
                    else
                    {
                        if (request.providerCode == "VTE")
                            request.providerCode = "DataVTT";
                        else if (request.providerCode == "VNA")
                            request.providerCode = "DataVNP";
                        else if (request.providerCode == "VMS")
                            request.providerCode = "DataVMS";
                    }


                    responseMessage.TransCodeProvider = topupRequestLog.TransCode;
                    var result = await CallApi(request, providerInfo);
                    if (result == null)
                    {
                        _logger.LogWarning(
                            $"ImediaConnector result is null: {topupRequestLog.ProviderCode}-{topupRequestLog.TransCode}");
                        responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                        responseMessage.ResponseMessage =
                            "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                        return responseMessage;
                    }

                    topupRequestLog.ModifiedDate = DateTime.Now;
                    topupRequestLog.ResponseInfo = request.ToJson();

                    _logger.Log(LogLevel.Information,
                        $"ImediaConnector return: {topupRequestLog.ProviderCode}-{topupRequestLog.TransCode}-{result.ToJson()}");
                    responseMessage.ProviderResponseCode = result?.errorCode.ToString();
                    responseMessage.ProviderResponseMessage = result?.errorMessage;
                    if (result.errorCode == 0)
                    {
                        topupRequestLog.Status = TransRequestStatus.Success;
                        responseMessage.ResponseCode = ResponseCodeConst.Success;
                        responseMessage.ProviderResponseTransCode = result.sysTransId;
                        responseMessage.ResponseMessage = "Giao dịch thành công";
                        if (result.accRealType != null)
                        {
                            responseMessage.ReceiverType = result.accRealType switch
                            {
                                0 => "TT",
                                1 => "TS",
                                _ => responseMessage.ReceiverType
                            };
                        }
                    }
                    else
                    {
                        if (result.errorCode == -1)
                        {
                            await GetToken(providerInfo, true);
                            responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                            responseMessage.ResponseMessage =
                                "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                        }
                        else
                        {
                            var arrayErrors =
                                _topupGatewayService.ConvertArrayCode(providerInfo.ExtraInfo ?? string.Empty);
                            if (arrayErrors.Contains(result.errorCode))
                            {
                                var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(
                                    ProviderConst.IMEDIA,
                                    result.errorCode.ToString(), topupRequestLog.TransCode);
                                topupRequestLog.Status = TransRequestStatus.Fail;
                                responseMessage.ResponseCode =
                                    reResult != null ? reResult.ResponseCode : ResponseCodeConst.Error;
                                responseMessage.ResponseMessage =
                                    reResult != null ? reResult.ResponseName : result.errorMessage;
                            }
                            else
                            {
                                topupRequestLog.Status = TransRequestStatus.Timeout;
                                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                                responseMessage.ResponseMessage =
                                    "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                            }
                        }
                    }

                    await _topupGatewayService.TopupRequestLogUpdateAsync(topupRequestLog);

                    return responseMessage;
                }
            }
            catch (Exception ex)
            {
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                responseMessage.ResponseMessage =
                    "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                responseMessage.Exception = ex.Message;
                return responseMessage;
            }
        }

        public async Task<MessageResponseBase> TransactionCheckAsync(string providerCode, string transCodeToCheck,
            string transCode, string serviceCode = null, ProviderInfoDto providerInfo = null)
        {
            try
            {
                //var q = transCodeToCheck.DecryptTripleDes();
                _logger.LogInformation(
                    $"{transCodeToCheck}-{transCode}-{providerCode} ImediaConnector check request: " + transCode);

                var responseMessage = new MessageResponseBase();

                if (providerInfo == null)
                    providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(providerCode);

                if (providerInfo == null ||
                    !_topupGatewayService.ValidConnector(ProviderConst.IMEDIA, providerInfo.ProviderCode))
                {
                    _logger.LogError(
                        $"{transCodeToCheck}-{transCode}-{providerCode}-ImediaConnector ProviderConnector not valid");
                    return new MessageResponseBase
                    {
                        ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult,
                        ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ"
                    };
                }

                // if (_token == null)
                //     IsLogin(providerInfo);

                int operation = 1300;
                if (serviceCode == ServiceCodes.PIN_CODE || serviceCode == ServiceCodes.PIN_DATA ||
                    serviceCode == ServiceCodes.PIN_GAME)
                    operation = 1100;

                var request = new ImediaRequest
                {
                    username = providerInfo.Username,
                    merchantPass = providerInfo.Password,
                    requestID = transCodeToCheck,
                    accountType = "0",
                    operation = operation,
                    keyBirthdayTime = operation == 1100 ? providerInfo.PublicKey.Split('|')[0] : null,
                };

                ImediaResponse result = await CallApi(request, providerInfo);
                _logger.Log(LogLevel.Information,
                    $"{providerCode}-{transCodeToCheck} ImediaConnector check return: {transCode}-{transCodeToCheck}-{result.ToJson()}");
                //responseMessage.ExtraInfo = string.Join("|", result.errorCode, result.errorMessage);
                if (result.errorCode == 0)
                {
                    responseMessage.ResponseCode = ResponseCodeConst.Success;
                    responseMessage.ResponseMessage = "Giao dịch thành công";
                    responseMessage.ProviderResponseTransCode = result.sysTransId;
                    if (result.accRealType != null)
                    {
                        responseMessage.ReceiverType = result.accRealType switch
                        {
                            0 => "TT",
                            1 => "TS",
                            _ => responseMessage.ReceiverType
                        };
                    }

                    if (operation == 1100)
                    {
                        try
                        {
                            var cardList = result.products.First()
                                .softpins.Select(card => new CardRequestResponseDto
                                {
                                    CardCode = Cryptography
                                        .DecryptCodeImedia(card.softpinPinCode, providerInfo.PublicKey.Split('|')[1])
                                        .EncryptTripDes(),
                                    Serial = card.softpinSerial,
                                    ExpiredDate = DateTime.ParseExact(card.expiryDate, "yyyy/MM/dd HH:mm:ss",
                                        CultureInfo.InvariantCulture), //GetExpireDate(cardText.ExpriedDate),
                                    ExpireDate = DateTime.ParseExact(card.expiryDate, "yyyy/MM/dd HH:mm:ss",
                                        CultureInfo.InvariantCulture).ToString("dd/MM/yyyy"),
                                    CardValue = "", //chỗ này sao k có mệnh giá
                                })
                                .ToList();

                            responseMessage.Payload = cardList;
                        }
                        catch (Exception e)
                        {
                            _logger.LogError($"transCodeToCheck= {transCodeToCheck}|Error parsing cards: {e.Message}");
                        }
                    }
                }
                else
                {
                    if (result.errorCode == -1)
                    {
                        await GetToken(providerInfo, true);
                        responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                        responseMessage.ResponseMessage =
                            "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                    }
                    else
                    {
                        var arrayErrors = _topupGatewayService.ConvertArrayCode(providerInfo.ExtraInfo ?? string.Empty);
                        if (arrayErrors.Contains(result.errorCode))
                        {
                            var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.IMEDIA,
                            result.errorCode.ToString(), transCode);
                            responseMessage.ResponseCode = reResult != null ? reResult.ResponseCode : ResponseCodeConst.ResponseCode_WaitForResult;
                            responseMessage.ResponseMessage = reResult != null ? reResult.ResponseName : result.errorMessage;
                        }
                        else
                        {
                            responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                            responseMessage.ResponseMessage =
                                "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                        }
                    }
                }


                responseMessage.ProviderResponseCode = result?.errorCode.ToString();
                responseMessage.ProviderResponseMessage = result?.errorMessage;
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

            if (!_topupGatewayService.ValidConnector(ProviderConst.IMEDIA, providerInfo.ProviderCode))
            {
                _logger.LogError(
                    $"{cardRequestLog.ProviderCode}-{cardRequestLog.TransCode}-{providerInfo.ProviderCode}-ImediaConnector ProviderConnector not valid");
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                };
            }

            // if (_token == null)
            //     IsLogin(providerInfo);


            var providerService =
                providerInfo.ProviderServices.Find(p => p.ProductCode == cardRequestLog.ProductCode);

            var request = new ImediaRequest
            {
                requestID = cardRequestLog.TransCode,
                username = providerInfo.Username,
                merchantPass = providerInfo.Password,
                operation = 1000,
                accountType = "0",
                providerCode = cardRequestLog.ProviderCode,
                buyItems = new buyItems[1]
                {
                    new buyItems()
                    {
                        productId = Convert.ToInt32(providerService.ServiceCode),
                        quantity = cardRequestLog.Quantity,
                    }
                },
                keyBirthdayTime = providerInfo.PublicKey.Split('|')[0],
            };
            responseMessage.TransCodeProvider = cardRequestLog.TransCode;

            _logger.LogInformation("Card object send: " + request.ToJson());

            ImediaResponse result = await CallApi(request, providerInfo);


            cardRequestLog.ModifiedDate = DateTime.Now;
            cardRequestLog.ResponseInfo = result.ToJson();
            _logger.Log(LogLevel.Information,
                $"ImediaConnector Card return: {providerInfo.ProviderCode}-{cardRequestLog.TransCode}-{cardRequestLog.TransRef}-{result.ToJson()}");
            if (result.errorCode == 0)
            {
                responseMessage.ResponseCode = ResponseCodeConst.Success;
                responseMessage.ProviderResponseTransCode = result.sysTransId;
                responseMessage.ResponseMessage = "Giao dịch thành công";
                cardRequestLog.Status = TransRequestStatus.Success;
                try
                {
                    var cardList = new List<CardRequestResponseDto>();
                    foreach (var card in result.products.First().softpins)
                    {
                        cardList.Add(new CardRequestResponseDto
                        {
                            CardCode = Cryptography.DecryptCodeImedia(card.softpinPinCode,
                                providerInfo.PublicKey.Split('|')[1]),
                            Serial = card.softpinSerial,
                            ExpiredDate = DateTime.ParseExact(card.expiryDate, "yyyy/MM/dd HH:mm:ss",
                                CultureInfo.InvariantCulture), //GetExpireDate(cardText.ExpriedDate),
                            ExpireDate = DateTime
                                .ParseExact(card.expiryDate, "yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture)
                                .ToString("dd/MM/yyyy"),
                            CardValue = cardRequestLog.TransAmount.ToString(CultureInfo.InvariantCulture),
                        });
                    }

                    responseMessage.Payload = cardList;
                }
                catch (Exception e)
                {
                    _logger.LogError($"{cardRequestLog.TransCode} Error parsing cards: " + e.Message);
                }
            }
            else
            {
                var arrayErrors = _topupGatewayService.ConvertArrayCode(providerInfo.ExtraInfo ?? string.Empty);
                if (arrayErrors.Contains(result.errorCode))
                {
                    var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.IMEDIA,
                        result.errorCode.ToString(), cardRequestLog.TransCode);
                    cardRequestLog.Status = TransRequestStatus.Fail;
                    responseMessage.ResponseCode = reResult != null ? reResult.ResponseCode : ResponseCodeConst.Error;
                    responseMessage.ResponseMessage = reResult != null ? reResult.ResponseName : result.errorMessage;
                }
                else
                {
                    cardRequestLog.Status = TransRequestStatus.Timeout;
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    // var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.IMEDIA,
                    //     result.errorCode.ToString(), cardRequestLog.TransCode);
                    responseMessage.ResponseMessage =
                        "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                    //responseMessage.ResponseCode=reResult != null ? reResult.ReponseCode : ResponseCodeConst.Error;
                }

                responseMessage.ProviderResponseCode = result?.errorCode.ToString();
                responseMessage.ProviderResponseMessage = result?.errorMessage;
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


            if (!_topupGatewayService.ValidConnector(ProviderConst.IMEDIA, providerInfo.ProviderCode))
            {
                _logger.LogError($"{transCode}-{providerCode}-ImediaConnector ProviderConnector not valid");
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                };
            }

            // if (_token == null)
            //     IsLogin(providerInfo);

            try
            {
                var svc = new IMediaIIoup.TopupInterfaceClient(providerInfo.ApiUrl);
                svc.Endpoint.Binding.SendTimeout = TimeSpan.FromSeconds(providerInfo.Timeout);
                //var index = 0;
                //while (true)
                //{
                var token = await GetToken(providerInfo);
                _logger.LogInformation($"LayToken:{token}");
                var response = await svc.queryBalanceAsync(providerInfo.Username,
                    DateTime.Now.ToString("yyyyMMddHHmmssfff"), token);
                _logger.LogInformation(
                    $"{providerCode} Call Api Balance : " + (response != null ? response.ToJson() : ""));
                await svc.CloseAsync();
                var result = response.ConvertTo<ImediaResponse>();
                // if (result.errorCode == -1)
                // {
                //     IsLogin(providerInfo);
                //     index += 1;
                //     continue;
                // }

                responseMessage.ResponseCode = ResponseCodeConst.Success;
                responseMessage.ResponseMessage = "Giao dịch thành công";
                responseMessage.Payload = result.dataValue.ToString();
                //break;
                //}
            }
            catch (Exception ex)
            {
                _logger.LogError($"{transCode} Balance exception: " + ex.Message);
                responseMessage.Payload = 0;
                responseMessage.Exception = ex.Message;
            }

            return responseMessage;
        }

        public Task<MessageResponseBase> DepositAsync(DepositRequestDto request)
        {
            throw new NotImplementedException();
        }

        public async Task<MessageResponseBase> PayBillAsync(PayBillRequestLogDto payBillRequestLog)
        {
            _logger.Log(LogLevel.Information,
                $"{payBillRequestLog.TransCode} Get Paybill request: " + payBillRequestLog.ToJson());

            var responseMessage = new MessageResponseBase();

            var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(payBillRequestLog.ProviderCode);

            if (providerInfo == null)
                return responseMessage;

            if (_topupGatewayService.ValidConnector(ProviderConst.IMEDIA, providerInfo.ProviderCode))
            {
                _logger.LogError(
                    $"{payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{providerInfo.ProviderCode}- ImediaConnector ProviderConnector not valid");
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                };
            }

            // if (_token == null)
            //     IsLogin(providerInfo);

            var providerService =
                providerInfo.ProviderServices.Find(p => p.ProductCode == payBillRequestLog.ProductCode);
            var serviceCode = string.Empty;
            if (providerService != null)
                serviceCode = providerService.ServiceCode;
            else
                _logger.LogWarning(
                    $"{payBillRequestLog.TransCode} ProviderService with ProductCode [{payBillRequestLog.ProductCode}] is null");
            var request = new ImediaRequest
            {
                accountType = "1",
                username = providerInfo.Username,
                merchantPass = providerInfo.Password,
                requestID = payBillRequestLog.TransCode,
                targetAccount = payBillRequestLog.ReceiverInfo,
                topupAmount = Convert.ToInt32(payBillRequestLog.TransAmount),
                providerCode = serviceCode,
                operation = 1200,
            };

            responseMessage.TransCodeProvider = payBillRequestLog.TransCode;
            ImediaResponse result = await CallApi(request, providerInfo);
            payBillRequestLog.ModifiedDate = DateTime.Now;
            payBillRequestLog.ResponseInfo = request.ToJson();
            _logger.Log(LogLevel.Information,
                $"{providerInfo.ProviderCode}-{payBillRequestLog.TransCode} Paybill return: {payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{result.ToJson()}");

            if (result.errorCode == 0)
            {
                payBillRequestLog.Status = TransRequestStatus.Success;
                responseMessage.ResponseCode = ResponseCodeConst.Success;
                responseMessage.ProviderResponseTransCode = result.sysTransId;
                responseMessage.ResponseMessage = "Giao dịch thành công";
            }
            else
            {
                var arrayErrors = _topupGatewayService.ConvertArrayCode(providerInfo.ExtraInfo ?? string.Empty);
                if (arrayErrors.Contains(result.errorCode))
                {
                    var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.IMEDIA,
                        result.errorCode.ToString(), payBillRequestLog.TransCode);
                    payBillRequestLog.Status = TransRequestStatus.Fail;
                    responseMessage.ResponseCode = reResult != null ? reResult.ResponseCode : ResponseCodeConst.Error;
                    responseMessage.ResponseMessage = reResult != null ? reResult.ResponseName : result.errorMessage;
                }
                else
                {
                    // var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.IMEDIA,
                    //     result.errorCode.ToString(), payBillRequestLog.TransCode);
                    payBillRequestLog.Status = TransRequestStatus.Timeout;
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage =
                        "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                }
            }

            responseMessage.ProviderResponseCode = result?.errorCode.ToString();
            responseMessage.ProviderResponseMessage = result?.errorMessage;
            await _topupGatewayService.PayBillRequestLogUpdateAsync(payBillRequestLog);

            return responseMessage;
        }

        private async Task<ImediaResponse> CallApi(ImediaRequest request, ProviderInfoDto providerInfo,
            bool isLogin = false)
        {
            ImediaResponse result;
            try
            {
                string sign;
                if (isLogin)
                {
                    sign = Sign(string.Join("|", request.username, request.merchantPass),
                        "./" + providerInfo.PrivateKeyFile);
                }
                else
                {
                    request.token = await GetToken(providerInfo);
                    sign = Sign(string.Join("|", request.username, request.requestID, request.token,
                        request.operation), "./" + providerInfo.PrivateKeyFile);
                }

                request.signature = sign;

                using (_logger.BeginScope("Send request to provider"))
                {
                    _logger.LogInformation("ImediaConnector request: " + request.ToJson());
                    try
                    {
                        //Disable validate SSL
                        ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
                        var scv = new IMediaIIoup.TopupInterfaceClient(providerInfo.ApiUrl);
                        scv.Endpoint.Binding.SendTimeout = TimeSpan.FromSeconds(providerInfo.Timeout);
                        var response = await scv.requestHandleAsync(request.ToJson());
                        _logger.LogInformation($"{request.requestID}-{request.operation} ImediaConnector response: " +
                                               (response != null ? response.ToJson() : ""));
                        await scv.CloseAsync();
                        result = response.FromJson<ImediaResponse>();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"{request.requestID} Topup exception: " + ex.Message);
                        result = new ImediaResponse
                        {
                            errorCode = 501102, //Tự quy định mã này cho trường hợp timeout.
                            errorMessage = ex.Message
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"{request.requestID} CallApi Exception : " + ex.Message);
                result = new ImediaResponse
                {
                    errorCode = 501102,
                    errorMessage = ex.Message
                };
            }

            return result;
        }

        private async Task<string> GetToken(ProviderInfoDto providerInfo, bool reLogin = false)
        {
            try
            {
                var key = "PayGate_ProviderToken:Items:IMEDIA_TOKEN";
                var tokenCache = await _cacheManager.GetEntity<TokenInfo>(key);
                if (tokenCache != null && string.IsNullOrEmpty(tokenCache.Token) && reLogin == false)
                {
                    _logger.Log(LogLevel.Information, $"GetTokenFromCache: {tokenCache}");
                    return tokenCache.Token;
                }

                var request = new ImediaRequest
                {
                    username = providerInfo.Username,
                    merchantPass = providerInfo.Password,
                    requestID = DateTime.Now.ToString("ddmmyyyyhhmmss") + new Random().Next(0, 10),
                    accountType = "0",
                    operation = 1400
                };

                var result = await CallApi(request, providerInfo, true);
                _logger.Log(LogLevel.Information, $"ImediaConnector login return: {result.ToJson()}");

                if (result.errorCode != 0 || string.IsNullOrEmpty(result.token)) return null;
                var token = result.token;
                var obj = new TokenInfo
                {
                    Token = token,
                    ProviderCode = ProviderConst.IMEDIA,
                    RequestDate = DateTime.UtcNow
                };
                await _cacheManager.AddEntity(key, obj, TimeSpan.FromHours(22));
                return token;
            }
            catch (Exception e)
            {
                _logger.LogError($"GetToken error:{e}");
                return null;
            }
        }

        private string Sign(string dataToSign, string privateFile)
        {
            //todo Cache privateKey;

            var privateKey = File.ReadAllText("files/" + privateFile);
            var privateKeyBlocks = privateKey.Split("-", StringSplitOptions.RemoveEmptyEntries);
            var privateKeyBytes = Convert.FromBase64String(privateKeyBlocks[1]);

            using var rsa = RSA.Create();

            if (privateKeyBlocks[0] == "BEGIN PRIVATE KEY")
                rsa.ImportPkcs8PrivateKey(privateKeyBytes, out _);
            else if (privateKeyBlocks[0] == "BEGIN RSA PRIVATE KEY") rsa.ImportRSAPrivateKey(privateKeyBytes, out _);

            var sig = rsa.SignData(
                Encoding.ASCII.GetBytes(dataToSign),
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            var signature = Convert.ToBase64String(sig);

            return signature;
        }

        // private DateTime getExpireDate(string expireDate)
        // {
        //     try
        //     {
        //         if (string.IsNullOrEmpty(expireDate))
        //             return new DateTime(DateTime.Now.AddYears(2).Year, 12, 31);
        //
        //         var s = expireDate.Split('/', '-');
        //         return new DateTime(Convert.ToInt32(s[0]), Convert.ToInt32(s[1]), Convert.ToInt32(s[2]));
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

        private BasicHttpBinding getBasicHttpBinding()
        {
            BasicHttpBinding binding = new BasicHttpBinding();
            binding.MaxBufferSize = int.MaxValue;
            binding.ReaderQuotas = System.Xml.XmlDictionaryReaderQuotas.Max;
            binding.MaxReceivedMessageSize = int.MaxValue;
            binding.AllowCookies = true;
            binding.Security.Mode = BasicHttpSecurityMode.Transport;
            binding.TransferMode = TransferMode.Buffered;
            return binding;
        }
    }
}