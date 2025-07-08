using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Topup.Contracts.Commands.Commons;
using Topup.Contracts.Requests.Commons;
using Topup.Shared;
using Topup.Shared.CacheManager;
using Topup.Shared.Dtos;
using Topup.Shared.Utils;
using Topup.TopupGw.Contacts.ApiRequests;
using Topup.TopupGw.Contacts.Dtos;
using Topup.TopupGw.Contacts.Enums;
using Topup.TopupGw.Domains.BusinessServices;
using MassTransit;
using Microsoft.Extensions.Logging;


using ServiceStack;


namespace Topup.TopupGw.Components.Connectors.Vinnet
{
    public class VinnetConnector : GatewayConnectorBase
    {

        private readonly ICacheManager _cacheManager;
        private readonly ILogger<VinnetConnector> _logger;
        private readonly IBus _bus;

        public VinnetConnector(ITopupGatewayService topupGatewayService,
            ILogger<VinnetConnector> logger, ICacheManager cacheManager, IBus bus)
            : base(topupGatewayService)
        {
            _logger = logger;
            _cacheManager = cacheManager;
            _bus = bus;
        }

        public override async Task<MessageResponseBase> TopupAsync(TopupRequestLogDto topupRequestLog,
            ProviderInfoDto providerInfo)
        {
            var responseMessage = new MessageResponseBase();
            try
            {
                using (_logger.BeginScope(topupRequestLog.TransCode))
                {
                    if (!TopupGatewayService.ValidConnector(ProviderConst.VINNET, providerInfo.ProviderCode))
                    {
                        return new MessageResponseBase
                        {
                            ResponseCode = ResponseCodeConst.Error,
                            ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                        };
                    }

                    var product = providerInfo.ProviderServices.Find(p => p.ProductCode == topupRequestLog.ProductCode);
                    if (product == null)
                    {
                        _logger.LogInformation($"{topupRequestLog.TransCode} - {topupRequestLog.TransRef} - {topupRequestLog.ProviderCode} - VinnetConnector ProviderConnector not config productCode= {topupRequestLog.ProductCode}");
                        return new MessageResponseBase
                        {
                            ResponseCode = ResponseCodeConst.Error,
                            ResponseMessage = "Giao dịch lỗi. Chưa cấu hình sản phẩm"
                        };
                    }
                    string type = "TT";
                    var serviceCode = product.ServiceCode.Split('|');
                    if (serviceCode.Length >= 2)
                        type = serviceCode[1];

                    var dtoReq = new reqDataDto()
                    {
                        quantity = 1,
                        recipient = topupRequestLog.ReceiverInfo,
                        recipientType = type,
                        referCode = string.Empty,
                        serviceCode = serviceCode[0],
                        serviceItem = new itemServiceDto()
                        {
                            itemValue = topupRequestLog.TransAmount,
                            itemCode = product.ServiceName,
                            description = string.Empty,
                        }
                    };
                    string reqJson = dtoReq.ToJson();                    
                    var data = new vinnetRequest
                    {
                        merchantCode = providerInfo.ApiUser,
                        reqUuid = topupRequestLog.TransCode,
                        reqData = Encrypt(reqJson, providerInfo.PublicKeyFile),
                    };
                    var plainText = $"{providerInfo.ApiUser}{topupRequestLog.TransCode}{data.reqData}";
                    data.sign = Sign(plainText, providerInfo.PrivateKeyFile);
                    var result = await CallApiVinnet(providerInfo, "payservice", data.ToJson(), topupRequestLog.TransCode);
                    if (result == null)
                    {
                        _logger.LogWarning($"VinnetConnector result is null: {topupRequestLog.ProviderCode}-{topupRequestLog.TransCode}");
                        responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                        responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                        topupRequestLog.Status = TransRequestStatus.Timeout;
                        await TopupGatewayService.TopupRequestLogUpdateAsync(topupRequestLog);
                        return responseMessage;
                    }

                    topupRequestLog.ModifiedDate = DateTime.Now;
                    _logger.LogInformation($"VinnetConnector return: {topupRequestLog.ProviderCode}-{topupRequestLog.TransCode}-{result.ToJson()}");
                    responseMessage.ProviderResponseCode = result?.resCode;
                    responseMessage.ProviderResponseMessage = result?.resMesg;
                    if (result.resCode == ResponseCodeConst.Error)
                    {
                        topupRequestLog.Status = TransRequestStatus.Success;
                        responseMessage.ResponseCode = ResponseCodeConst.Success;
                        var repData = Decrypt(result.resData, providerInfo.PrivateKeyFile);
                        var dataRep = repData.FromJson<resDataReponse>();
                        responseMessage.ResponseMessage = "Giao dịch thành công";
                        if (!string.IsNullOrEmpty(dataRep.recipientType))
                            responseMessage.ReceiverType = dataRep.recipientType;
                    }
                    else
                    {
                        if ((providerInfo.ExtraInfo ?? string.Empty).Split(',', ';', '|').Contains(result.resCode))
                        {
                            var reResult = await TopupGatewayService.GetResponseMassageCacheAsync(
                                    ProviderConst.VINNET, result.resCode, topupRequestLog.TransCode);
                            topupRequestLog.Status = TransRequestStatus.Fail;
                            responseMessage.ResponseCode =
                                reResult != null ? reResult.ResponseCode : ResponseCodeConst.ResponseCode_ErrorProvider;
                            responseMessage.ResponseMessage =
                                reResult != null ? reResult.ResponseName :"Giao dịch lỗi phía NCC";
                        }
                        else
                        {
                            topupRequestLog.Status = TransRequestStatus.Timeout;
                            responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                            responseMessage.ResponseMessage = "Giao dịch chưa có kết quả";
                        }
                    }

                    await TopupGatewayService.TopupRequestLogUpdateAsync(topupRequestLog);
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

        public override async Task<MessageResponseBase> TransactionCheckAsync(string providerCode, string transCodeToCheck,
            string transCode, string serviceCode = null, ProviderInfoDto providerInfo = null)
        {
            try
            {
                _logger.LogInformation($"{transCodeToCheck} - VinnetConnector Check request: " + transCode);
                var responseMessage = new MessageResponseBase();

                if (providerInfo == null)
                    providerInfo = await TopupGatewayService.ProviderInfoCacheGetAsync(providerCode);

                if (providerInfo == null ||
                    !TopupGatewayService.ValidConnector(ProviderConst.VINNET, providerInfo.ProviderCode))
                {
                    _logger.LogError($"{transCode}-{providerCode} - VinnetConnector ProviderConnector not valid");
                    return new MessageResponseBase
                    {
                        ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult,
                        ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ"
                    };
                }

                var dtoReq = new checkVinnetDto()
                {
                    refReqUuid = transCodeToCheck
                };
                string reqJson = dtoReq.ToJson();

                var data = new vinnetRequest()
                {
                    merchantCode = providerInfo.ApiUser,
                    reqUuid = transCodeToCheck,
                    reqData = Encrypt(reqJson, providerInfo.PublicKeyFile),
                };
                var plainText = $"{providerInfo.ApiUser}{data.reqUuid}{data.reqData}";
                data.sign = Sign(plainText, providerInfo.PrivateKeyFile);
                _logger.LogInformation("{TransCode} VinnetConnector send: {Data}", transCodeToCheck, data.ToJson());
                var result = await CallApiVinnet(providerInfo, "checktransaction", data.ToJson(), transCodeToCheck);
                _logger.LogInformation("VinnetConnector GetCard_CallApi return: {TransCode} - {TransRef} - {Return}",
                    transCodeToCheck, transCode, result.ToJson());

                if (result != null)
                {
                    responseMessage.ProviderResponseCode = result.resCode;
                    responseMessage.ProviderResponseMessage = result.resMesg;
                    if (result.resCode == ResponseCodeConst.Error)
                    {
                        var repDataDecrypt = Decrypt(result.resData, providerInfo.PrivateKeyFile);
                        var resData = repDataDecrypt.FromJson<resDataReponse>();
                        if (resData.status == 1)
                        {
                            responseMessage.ResponseCode = ResponseCodeConst.Success;
                            responseMessage.ResponseMessage = "Giao dịch thành công";
                            try
                            {
                                if (resData.cardItems != null)
                                {
                                    var cardList = new List<CardRequestResponseDto>();
                                    foreach (var card in resData.cardItems)
                                        cardList.Add(new CardRequestResponseDto
                                        {
                                            CardCode = card.pinCode.EncryptTripDes(),
                                            Serial = card.serialNo,
                                            ExpireDate = card.expiryDate,
                                            ExpiredDate = DateTime.ParseExact(card.expiryDate, "dd-MM-yyyy",
                                                CultureInfo.InvariantCulture),
                                        });
                                    responseMessage.Payload = cardList;
                                    if (cardList.Count == 0)
                                        await SendNoti(new CardRequestLogDto()
                                        {
                                            TransCode = transCodeToCheck,
                                            ProviderCode = providerCode,
                                            ProductCode = string.Empty,
                                            PartnerCode = "",
                                        }, providerInfo, "Check lại thẻ");
                                }
                            }
                            catch (Exception exData)
                            {
                                _logger.LogError($"{transCodeToCheck} Exception: {exData}");
                            }
                        }
                        else if (resData.status == 3)
                        {
                            responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_Failed;
                            responseMessage.ResponseMessage = "Giao dịch thất bại.";
                        }
                        else
                        {
                            responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                            responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả xử lý.";
                        }
                    }
                    else
                    {
                        responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                        responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả xử lý.";
                    }
                }

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

        public override async Task<MessageResponseBase> CardGetByBatchAsync(CardRequestLogDto cardRequestLog)
        {
            _logger.LogInformation($"{cardRequestLog.TransCode} VinnetConnector card request: " +
                                   cardRequestLog.ToJson());
            var responseMessage = new MessageResponseBase();
            var providerInfo = await TopupGatewayService.ProviderInfoCacheGetAsync(cardRequestLog.ProviderCode);

            if (providerInfo == null)
                return responseMessage;

            if (!TopupGatewayService.ValidConnector(ProviderConst.VINNET, providerInfo.ProviderCode))
            {
                _logger.LogError($"{cardRequestLog.TransCode}-{cardRequestLog.TransRef}-{providerInfo.ProviderCode} - VinnetConnector ProviderConnector not valid");
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                };
            }

            var product = providerInfo.ProviderServices.Find(p => p.ProductCode == cardRequestLog.ProductCode);
            if (product == null)
            {
                _logger.LogError($"{cardRequestLog.TransCode} - {cardRequestLog.TransRef} - {cardRequestLog.ProviderCode} - VinnetConnector ProviderConnector not config productCode= {cardRequestLog.ProductCode}");
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Giao dịch lỗi. Chưa cấu hình sản phẩm"
                };
            }

            string serviceCode = product.ServiceCode;
            var dtoReq = new reqDataDto()
            {
                quantity = cardRequestLog.Quantity,
                recipient = string.Empty,
                recipientType = string.Empty,
                referCode = string.Empty,
                serviceCode = product.ServiceCode.Split('|')[0],
                serviceItem = new itemServiceDto()
                {
                    description = string.Empty,
                    itemValue = Convert.ToDouble(cardRequestLog.TransAmount),
                    itemCode = product.ServiceName
                }
            };
            
            responseMessage.TransCodeProvider = cardRequestLog.TransCode;
            var data = new vinnetRequest
            {
                merchantCode = providerInfo.ApiUser,
                reqUuid = cardRequestLog.TransCode,
                reqData = Encrypt(dtoReq.ToJson(), providerInfo.PublicKeyFile),
            };
            var plainText = $"{providerInfo.ApiUser}{cardRequestLog.TransCode}{data.reqData}";
            data.sign = Sign(plainText, providerInfo.PrivateKeyFile);

            _logger.LogInformation("{TransCode} VinnetConnector send: {Data}", cardRequestLog.TransCode, data.ToJson());
            var result = await CallApiVinnet(providerInfo, "payservice", data.ToJson(),
                cardRequestLog.TransCode);
            _logger.LogInformation("VinnetConnector CallApi return: {TransCode} - {TransRef} - {Return}",
                cardRequestLog.TransCode, cardRequestLog.TransRef, result.ToJson());

            if (result == null)
            {
                _logger.LogInformation($"{cardRequestLog.TransCode} Error send request");
                cardRequestLog.Status = TransRequestStatus.Timeout;
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả xử lý.";
                await TopupGatewayService.CardRequestLogUpdateAsync(cardRequestLog);
                return responseMessage;
            }

            cardRequestLog.ResponseInfo = "";
            cardRequestLog.ModifiedDate = DateTime.Now;
            _logger.LogInformation("{ProviderCode} - {TransCode} - VinnetConnector return: {TransRef} - {Json}",
                cardRequestLog.ProviderCode, cardRequestLog.TransCode, cardRequestLog.TransRef, result.ToJson());

            responseMessage.ProviderResponseCode = result.resCode;
            responseMessage.ProviderResponseMessage = result.resMesg;
            if (result.resCode == ResponseCodeConst.Error)
            {
                cardRequestLog.Status = TransRequestStatus.Success;
                responseMessage.ResponseCode = ResponseCodeConst.Success;
                try
                {
                    var repData = Decrypt(result.resData, providerInfo.PrivateKeyFile);
                    var cardList = new List<CardRequestResponseDto>();
                    var dataRep = repData.FromJson<resDataReponse>();
                    foreach (var card in dataRep.cardItems)
                        cardList.Add(new CardRequestResponseDto
                        {
                            CardType = cardRequestLog.Vendor,
                            CardValue = cardRequestLog.TransAmount.ToString(),
                            CardCode = card.pinCode,
                            Serial = card.serialNo,
                            ExpireDate = card.expiryDate,
                            ExpiredDate = DateTime.ParseExact(card.expiryDate, "dd-MM-yyyy", CultureInfo.InvariantCulture),
                        });
                    responseMessage.Payload = cardList;
                    if (cardList.Count == 0)
                        await SendNoti(cardRequestLog, providerInfo, "Lấy mới thẻ cào");
                }
                catch (Exception e)
                {
                    _logger.LogError($"TransCode= {cardRequestLog.TransCode} VinnetConnector Error parsing cards: " +
                                     e.Message);
                }
            }
            else
            {
                var arrayErrors = (providerInfo.ExtraInfo ?? string.Empty).Split(';', '|', ',');
                if (arrayErrors.Contains(result.resCode))
                {
                    cardRequestLog.Status = TransRequestStatus.Fail;
                    responseMessage.ResponseCode = ResponseCodeConst.Error;
                    responseMessage.ResponseMessage = "Giao dịch không thành công";
                }
                else
                {
                    cardRequestLog.Status = TransRequestStatus.Timeout;
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả xử lý.";
                }
            }


            await TopupGatewayService.CardRequestLogUpdateAsync(cardRequestLog);

            return responseMessage;
        }

        public override async Task<MessageResponseBase> CheckBalanceAsync(string providerCode, string transCode)
        {
            _logger.LogInformation("{TransCode} Get balance request", transCode);
            var responseMessage = new MessageResponseBase();
            var providerInfo = await TopupGatewayService.ProviderInfoCacheGetAsync(providerCode);

            if (providerInfo == null)
                return responseMessage;

            if (!TopupGatewayService.ValidConnector(ProviderConst.VINNET, providerInfo.ProviderCode))
            {
                _logger.LogError("{ProviderCode} - {TransCode} - VinnetConnector ProviderConnector not valid", providerCode,
                    transCode);
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                };
            }

            var data = new vinnetRequest
            {
                merchantCode = providerInfo.ApiUser,
                reqUuid = Guid.NewGuid().ToString()
            };

            var dtoReq = new balanceReqDto();
            string reqJson = dtoReq.ToJson();
            data.reqData = Encrypt(reqJson, providerInfo.PublicKeyFile);
            var plainText = $"{providerInfo.ApiUser}{data.reqUuid}{data.reqData}";
            data.sign = Sign(plainText, providerInfo.PrivateKeyFile);
            //   _logger.LogInformation($"{transCode} Balance object send: {Data}", );
            var result = await CallApiVinnet(providerInfo, "merchantinfo", data.ToJson(), transCode);
            if (result != null)
            {
                _logger.LogInformation($"{transCode} Balance return: {transCode}-{result.ToJson()}");
                if (result.resCode == ResponseCodeConst.Error)
                {
                    responseMessage.ResponseCode = ResponseCodeConst.Success;
                    responseMessage.ResponseMessage = "Giao dịch thành công";
                    var repData = Decrypt(result.resData, providerInfo.PrivateKeyFile);
                    var balance = repData.FromJson<balanceResDto>();
                    responseMessage.Payload = balance.balance.ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    responseMessage.ResponseCode = ResponseCodeConst.Error;
                    responseMessage.ResponseMessage = result.resMesg;
                }
            }
            else
            {
                _logger.LogInformation("{TransCode} Error send request", transCode);
                responseMessage.ResponseMessage = "Lỗi kết nối nhà cung cấp";
            }

            return responseMessage;
        }

        public override async Task<ResponseMessageApi<object>> GetProductInfo(GetProviderProductInfo info)
        {
            try
            {
                var providerInfo = await TopupGatewayService.ProviderInfoCacheGetAsync(info.ProviderCode);

                if (info.ProviderType == "changekey")
                {
                    var dtoReq = new changeKeyReqDto()
                    {
                        oldMerchantKey = providerInfo.ApiPassword
                    };
                    string reqJson = dtoReq.ToJson();
                    var data = new vinnetRequest
                    {
                        merchantCode = providerInfo.ApiUser,
                        reqUuid = Guid.NewGuid().ToString(),
                        reqData = reqJson,
                    };
                    data.reqData = Encrypt(reqJson, providerInfo.PublicKeyFile);
                    var plainText = $"{providerInfo.ApiUser}{data.reqUuid}{data.reqData}";
                    data.sign = Sign(plainText, providerInfo.PrivateKeyFile);
                    var result = await CallApiVinnet(providerInfo, "changekey", data.ToJson(), data.reqUuid);
                    if (result != null)
                    {
                        _logger.LogWarning($"VinnetConnector changekey : {result.ToJson()}");
                        var dataKey = Decrypt(result.resData, providerInfo.PrivateKeyFile);
                        return new ResponseMessageApi<object>()
                        {
                            Result = dataKey
                        };
                    }

                }
                else if (info.ProviderType == "queryservice")
                {
                    var dataKey = await GetService(info.AccountNo, providerInfo);
                    return new ResponseMessageApi<object>()
                    {
                        Result = dataKey
                    };
                }
                else
                {
                    string msg = "TT2MzxX03mGQoarSOQnIvvbmnvrAEjsIciLHxUhpFE2nTPIVZFU6zMUfMinfADURm9ueVNPOThZgQHdXaDk8nBeONr/n/Gb0vVWsSgb5Ly26DVQtT4izXKGMR8CAoqR/XHvjwGpwY8h/aVuqXeg6hg7eJMuRKYo9bQTFUv5RJGr1ZkMll8vPwAl8R4cAPmfaXMb/enkWi7Y15DXdJ9kM2P6sPDNMSeViO8Wbo4ghEKd3jry1ZSxnMmG49bnMi6H6i2RwYPZU9KSOXGRPUa0GyShOzu5LIXFaT/ajl5/W9kgdtJ85+pTUkp5kFjUgnkoRZURVCHC/ynC66UL6ZBuVXQ==BzSPBSoMBQf4hYC6CBmGBaMy5COr6zeAiSTHdKOIbAyl1WHDaF/+B+Ec9tXhku6d3fFZUsgbKGR6DeYpxRslsljFwuDeBBScak/gubVsX8d0FdRjvA3JCUTgFnCrDJdmD9h2E4sXxqSFv1q69KfVwIf04tv2z4YjDW2BFHhq+pDj76DOko5YGFujDhNd2WUyMRkx4zMEB3T7mx1VVc0WYVpIf7Qb3zov2tw1nroPWvxVmD0UGvY9JUxep0ZuX/Ugnk1LgxUY8VUw+DDwQ+oocOr/rDn4cDMpc7dRKiG5FsnyUqp+YvLkjmBtAbMtinRlltan1MVyOikx2iIhr5FJOQ==";
                    string addStr = Decrypt(msg, providerInfo.PrivateKeyFile);
                    return new ResponseMessageApi<object>()
                    {
                        Result = addStr
                    }; ;
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError($"{info.ProviderCode} GetProductInfo Exception : {ex}");
                return new ResponseMessageApi<object>()
                {
                    Result = ex.Message
                };
            }
        }

        private async Task<string> GetService(string service, ProviderInfoDto providerInfo)
        {
            var dtoReq = new serviceReqDto()
            {
                serviceCode = service,
                recipient = null
            };
            string reqJson = dtoReq.ToJson();
            var data = new vinnetRequest
            {
                merchantCode = providerInfo.ApiUser,
                reqUuid = Guid.NewGuid().ToString(),
                reqData = Encrypt(reqJson, providerInfo.PublicKeyFile),
            };
            var plainText = $"{providerInfo.ApiUser}{data.reqUuid}{data.reqData}";
            data.sign = Sign(plainText, providerInfo.PrivateKeyFile);
            var result = await CallApiVinnet(providerInfo, "queryservice", data.ToJson(), data.reqUuid);
            var arraySubs = result.resData.Split("==");
            string addStr = string.Empty;
            foreach (var aray in arraySubs)
            {
                if (!string.IsNullOrEmpty(aray))
                {
                    var strGen = Decrypt(aray + "==", providerInfo.PrivateKeyFile);
                    addStr = addStr + strGen;
                }
            }

            return addStr;
            //var viettelCards = addStr.FromJson<List<ViettelCards>>();
            //var repData = Decrypt(result.resData, providerInfo.PrivateKeyFile);
        }

        private async Task<vinnetReponse> CallApiVinnet(ProviderInfoDto providerInfo, string function, string request,
            string transCode)
        {
            string token = string.Empty;
            if (function != "changekey" && !string.IsNullOrEmpty(function))
            {
                token = await GetToken(providerInfo);
                if (string.IsNullOrEmpty(token))
                {
                    return new vinnetReponse()
                    {
                        resCode = "98",
                        resMesg = "Không lấy được token"
                    };
                }
            }

            var response = await CallApi(providerInfo, function, token, request, transCode);
            if (response.resCode is ResponseCodeConst.Success or "02" or "03")
            {
                token = await GetToken(providerInfo, reLogin: true);
                response = await CallApi(providerInfo, function, token, request, transCode);
            }
            return response;
        }

        private async Task<vinnetReponse> CallApi(ProviderInfoDto providerInfo, string function, string token, string request,
           string transCode)
        {
            try
            {
                string url = $"{providerInfo.ApiUrl}";
                var client = new JsonServiceClient(url)
                {
                    Timeout = TimeSpan.FromMinutes(providerInfo.Timeout),
                };
                if (function is "merchantinfo" or "queryservice"
                    or "payservice" or "checktransaction")
                    client.AddHeader("Authorization", token);
                var res = await client.PostAsync<vinnetReponse>("rest/merchant/" + function, request);
                _logger.LogInformation($"TransCode = {transCode} - Function = {function} - CallApi : {res.resCode}|{res.resMesg}");
                return res;
            }
            catch (Exception ex)
            {
                _logger.LogError($"TransCode = {transCode} - Function = {function} - CallApi Exception: {ex}");
                var reMessageData = ((WebServiceException)ex).ResponseBody;
                _logger.LogError($"TransCode = {transCode} - Function = {function} - CallApi : {reMessageData}");
                var msg = reMessageData.FromJson<vinnetReponse>();
                return msg;
            }
        }

        private async Task<string> GetToken(ProviderInfoDto providerInfo, bool reLogin = false)
        {
            try
            {
                var key = $"PayGate_ProviderToken:Items:{providerInfo.ProviderCode}";
                var tokenCache = await _cacheManager.GetEntity<TokenInfo>(key);
                if (tokenCache != null && !string.IsNullOrEmpty(tokenCache.Token) && reLogin == false)
                {
                    _logger.LogInformation($"GetTokenFromCache: {tokenCache}");
                    return tokenCache.Token;
                }

                var redData = new tokenReqDto()
                {
                    merchantKey = providerInfo.ApiPassword
                };
                var request = new vinnetRequest
                {
                    reqUuid = Guid.NewGuid().ToString(),
                    merchantCode = providerInfo.ApiUser,
                    reqData = Encrypt(redData.ToJson(), providerInfo.PublicKeyFile),
                };
                var plainText = $"{providerInfo.ApiUser}{request.reqUuid}{request.reqData}";
                request.sign = Sign(plainText, providerInfo.PrivateKeyFile);

                var result = await CallApi(providerInfo, "authen", string.Empty, request.ToJson(), request.merchantCode);
                _logger.LogInformation($"VinnetConnector login return : {result.ToJson()}");
                if (result.resCode == ResponseCodeConst.Error)
                {
                    var repData = Decrypt(result.resData, providerInfo.PrivateKeyFile);
                    var rs = repData.FromJson<vinetToken>();
                    var obj = new TokenInfo
                    {
                        Token = rs.token,
                        ProviderCode = providerInfo.ProviderCode,
                        RequestDate = DateTime.UtcNow
                    };
                    await _cacheManager.AddEntity(key, obj, TimeSpan.FromHours(1));
                    return rs.token;
                }
                return null;
            }
            catch (Exception e)
            {
                _logger.LogError($"GetToken error:{e}");
                return null;
            }
        }

        private static string Sign(string dataToSign, string privateFile)
        {
            var privateKeyText = File.ReadAllText("files/" + privateFile);
            var privateKeyBlocks = privateKeyText.Split("-", StringSplitOptions.RemoveEmptyEntries);
            var str = privateKeyBlocks[1].Replace("\r\n", "");
            var privateKeyBytes = Convert.FromBase64String(str);
            using var rsa = RSA.Create();

            if (privateKeyBlocks[0] == "BEGIN PRIVATE KEY")
                rsa.ImportPkcs8PrivateKey(privateKeyBytes, out _);

            else if (privateKeyBlocks[0] == "BEGIN RSA PRIVATE KEY") rsa.ImportRSAPrivateKey(privateKeyBytes, out _);
            var sig = rsa.SignData(
                Encoding.UTF8.GetBytes(dataToSign),
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            var signature = Convert.ToBase64String(sig);
            return signature;
        }

        private string Encrypt(string dataToSign, string publickey)
        {
            byte[] textBytes = Encoding.UTF8.GetBytes(dataToSign);
            string publicKeyString = File.ReadAllText("files/" + publickey);
            var rsaPublicKey = RSA.Create();
            rsaPublicKey.ImportFromPem(publicKeyString);
            byte[] bytesEncrypted = rsaPublicKey.Encrypt(textBytes, RSAEncryptionPadding.Pkcs1);
            return Convert.ToBase64String(bytesEncrypted);
        }

        private string Decrypt(string decryptedData, string privateFile)
        {
            try
            {
                var privateKeyText = File.ReadAllText("files/" + privateFile);
                var privateKeyBlocks = privateKeyText.Split("-", StringSplitOptions.RemoveEmptyEntries);
                var privateKeyBytes = Convert.FromBase64String(privateKeyBlocks[1]);
                using var rsa = RSA.Create();
                if (privateKeyBlocks[0] == "BEGIN PRIVATE KEY")
                    rsa.ImportPkcs8PrivateKey(privateKeyBytes, out _);
                else if (privateKeyBlocks[0] == "BEGIN RSA PRIVATE KEY") rsa.ImportRSAPrivateKey(privateKeyBytes, out _);
                var rsaPrivateKey = rsa.ExportParameters(true);

                var arraySubs = decryptedData.Split("==");
                string genStr = string.Empty;
                foreach (var aray in arraySubs)
                {
                    if (!string.IsNullOrEmpty(aray))
                    {
                        string intData = aray + "==";
                        intData = intData.Replace("\r", "").Replace("\n", "");
                        var bTemp = Convert.FromBase64String(intData);
                        var sq = rsa.Decrypt(bTemp, RSAEncryptionPadding.Pkcs1);
                        var decrypted = Encoding.UTF8.GetString(sq);
                        genStr = genStr + decrypted;
                    }
                }

                return genStr;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Decrypt Exception: {ex}");
                return string.Empty;
            }
        }

        private async Task SendNoti(CardRequestLogDto requestDto, ProviderInfoDto provider, string type)
        {
            try
            {
                if (!string.IsNullOrEmpty(provider.AlarmTeleChatId) && provider.IsAlarm)
                {
                    await _bus.Publish<SendBotMessageToGroup>(new
                    {
                        MessageType = BotMessageType.Wraning,
                        BotType = BotType.Private,
                        ChatId = provider.AlarmTeleChatId,
                        Module = "TopupGate",
                        Title = $"Cảnh báo không lấy được thẻ kênh : {provider.ProviderCode}",
                        Message =
                            $"Mã GD: {requestDto.TransCode}\n" +
                            $"Đại lý: {requestDto.PartnerCode}\n" +
                            $"Sản phẩm: {requestDto.ProductCode}\n" +
                            $"Loại: {type}\n" +
                            $"Kênh: {provider.ProviderCode}\n" +
                            $"Trạng thái: Thành công\n",
                        TimeStamp = DateTime.Now,
                        CorrelationId = Guid.NewGuid()
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"SendNoti_Exception :{ex}");
            }
        }

        #region ....Object

        internal class serviceReqDto
        {
            public string serviceCode { get; set; }

            public string recipient { get; set; }
        }
        internal class balanceReqDto
        {

        }

        internal class changeKeyReqDto
        {
            public string oldMerchantKey { get; set; }
        }

        internal class tokenReqDto
        {
            public string merchantKey { get; set; }
        }

        internal class balanceResDto
        {
            public double deposited { get; set; }
            public double spent { get; set; }
            public double balance { get; set; }
        }

        internal class checkVinnetDto
        {
            public string refReqUuid { get; set; }
        }

        internal class vinnetRequest
        {
            public string merchantCode { get; set; }
            public string reqUuid { get; set; }
            public string reqData { get; set; }
            public string sign { get; set; }
        }

        internal class reqDataDto
        {
            public string serviceCode { get; set; }

            public string recipient { get; set; }

            public string recipientType { get; set; }

            public string referCode { get; set; }

            public itemServiceDto serviceItem { get; set; }

            public int quantity { get; set; }

            public string Signature { get; set; }
        }

        internal class itemServiceDto
        {
            public double itemValue { get; set; }
            public string itemCode { get; set; }
            public string description { get; set; }
        }

        [DataContract]
        internal class vinnetReponse
        {
            [DataMember(Name = "reqUuid")] public string reqUuid { get; set; }
            [DataMember(Name = "resCode")] public string resCode { get; set; }
            [DataMember(Name = "resMesg")] public string resMesg { get; set; }
            [DataMember(Name = "resData")] public string resData { get; set; }
        }

        internal class resDataReponse
        {
            public int status { get; set; }
            public string recipientType { get; set; }
            public List<vinetItem> cardItems { get; set; }
            public string signature { get; set; }
        }

        internal class vinetItem
        {
            public string serialNo { get; set; }
            public string pinCode { get; set; }
            public string expiryDate { get; set; }
        }

        internal class vinetToken
        {
            public string token { get; set; }
        }

        #endregion
    }
}
