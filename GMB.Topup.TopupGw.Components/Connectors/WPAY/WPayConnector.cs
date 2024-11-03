using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using GMB.Topup.Shared;
using GMB.Topup.Shared.Dtos;
using GMB.Topup.Shared.Utils;
using GMB.Topup.TopupGw.Contacts.ApiRequests;
using GMB.Topup.TopupGw.Contacts.Dtos;
using GMB.Topup.TopupGw.Contacts.Enums;
using GMB.Topup.TopupGw.Domains.BusinessServices;
using MassTransit;
using Microsoft.Extensions.Logging;
using ServiceStack;

namespace GMB.Topup.TopupGw.Components.Connectors.WPay
{
    public class WPayConnector : IGatewayConnector
    {
        private readonly ITopupGatewayService _topupGatewayService;
        private readonly IBusControl _bus;
        private static UnicodeEncoding _encoder = new UnicodeEncoding();
        private readonly ILogger<WPayConnector> _logger;

        public WPayConnector(ITopupGatewayService topupGatewayService, ILogger<WPayConnector> logger,
            IBusControl bus)
        {
            _topupGatewayService = topupGatewayService;
            _logger = logger;
            _bus = bus;
        }

        public async Task<MessageResponseBase> TopupAsync(TopupRequestLogDto topupRequestLog,
            ProviderInfoDto providerInfo)
        {
            _logger.LogInformation($"{topupRequestLog.TransCode} WPayConnector topup request: " +
                                   topupRequestLog.ToJson());
            var responseMessage = new MessageResponseBase();
            try
            {
                if (!_topupGatewayService.ValidConnector(ProviderConst.WPAY, providerInfo.ProviderCode))
                {
                    _logger.LogError(
                        $"{topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{providerInfo.ProviderCode}-WPayConnector ProviderConnector not valid");
                    return new MessageResponseBase
                    {
                        ResponseCode = ResponseCodeConst.Error,
                        ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                    };
                }

                var resCodeInfo = providerInfo.ExtraInfo;
                var str = topupRequestLog.ProductCode.Split('_');
                string keyCode = topupRequestLog.ProductCode.Contains("TOPUP")
                    ? $"{str[0]}_{str[1]}"
                    : topupRequestLog.ProductCode;
                var providerService = providerInfo.ProviderServices.Find(p => p.ProductCode == keyCode);
                var telcoCode = string.Empty;
                if (providerService != null)
                    telcoCode = providerService.ServiceCode.Split('|')[0];
                else
                    _logger.LogInformation(
                        $"{topupRequestLog.TransCode}-{topupRequestLog.TransRef} ProviderService with ProductCode [{topupRequestLog.ProductCode}] is null");

                var data = new WPayRequest
                {
                    partnerCode = providerInfo.Username,
                    partnerTransId = topupRequestLog.TransCode,
                    telcoCode = telcoCode
                };

                var plainText = string.Empty;
                var function = string.Empty;
                if (topupRequestLog.ProductCode.Contains("TOPUP"))
                {
                    data.mobileNo = topupRequestLog.ReceiverInfo;
                    data.topupAmount = topupRequestLog.TransAmount;
                    plainText =
                        $"partnerCode={data.partnerCode}&partnerTransId={data.partnerTransId}&telcoCode={data.telcoCode}&mobileNo={data.mobileNo}&topupAmount={data.topupAmount}";
                    function = "/direct-topup";
                }
                else
                {
                    data.billingCode = topupRequestLog.ReceiverInfo;
                    data.paidAmount = long.Parse(topupRequestLog.TransAmount.ToString());
                    plainText =
                        $"partnerCode={data.partnerCode}&partnerTransId={data.partnerTransId}&productCode={data.productCode}&billingCode={data.billingCode}&paidAmount={data.paidAmount}";
                    function = "/pay-bill";
                }

                data.sign = Sign(plainText, providerInfo.PrivateKeyFile);
                responseMessage.TransCodeProvider = topupRequestLog.TransCode;
                _logger.LogInformation($"{topupRequestLog.TransCode} WPayConnector send: " + data.ToJson());
                var result = await CallApi(providerInfo.ApiUrl + function, data, providerInfo.Timeout);
                _logger.LogInformation(
                    $"CallApi return: {topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{result.ToJson()}");

                if (result != null)
                {
                    topupRequestLog.ResponseInfo = result.ToJson();
                    topupRequestLog.ModifiedDate = DateTime.Now;
                    responseMessage.ProviderResponseCode = result?.resCode;
                    responseMessage.ProviderResponseMessage = result?.resMessage;
                    _logger.LogInformation(
                        $"{topupRequestLog.ProviderCode}{topupRequestLog.TransCode} WPayConnector return: {topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{result.ToJson()}");
                    if (result.resCode == "00")
                    {
                        topupRequestLog.Status = TransRequestStatus.Success;
                        responseMessage.ProviderResponseTransCode = string.Empty;
                        responseMessage.ReceiverType = result.mobileType;
                        responseMessage.ResponseCode = ResponseCodeConst.Success;
                        responseMessage.ResponseMessage = "Giao dịch thành công";
                        responseMessage.ReceiverType = result.mobileType;
                    }
                    else if (resCodeInfo.Contains(result.resCode))
                    {
                        var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.WPAY,
                            result.resCode, topupRequestLog.TransCode);
                        topupRequestLog.Status = TransRequestStatus.Fail;
                        responseMessage.ResponseCode =
                            reResult != null ? reResult.ResponseCode : ResponseCodeConst.Error;
                        responseMessage.ResponseMessage = reResult != null ? reResult.ResponseName : result.resMessage;
                    }
                    else
                    {
                        //var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.WPAY, result.resMessage, topupRequestLog.TransCode);
                        topupRequestLog.Status = TransRequestStatus.Timeout;
                        responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                        responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả xử lý.";
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
            _logger.LogInformation($"{transCodeToCheck}  WPayConnector check request: " + transCodeToCheck + "|" +
                                   transCode);
            var responseMessage = new MessageResponseBase();
            try
            {
                if (providerInfo == null)
                    providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(providerCode);


                if (providerInfo == null ||
                    !_topupGatewayService.ValidConnector(ProviderConst.WPAY, providerInfo.ProviderCode))
                {
                    _logger.LogError($"{transCode}-{providerCode}- WPayConnector ProviderConnector not valid");
                    return new MessageResponseBase
                    {
                        ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult,
                        ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ"
                    };
                }

                string transType = "TU";
                if (serviceCode.Contains("TOPUP"))
                    transType = "TU";
                else if (serviceCode.Contains("PIN"))
                    transType = "BC";
                else transType = "VB";

                var data = new WPayRequest
                {
                    partnerCode = providerInfo.Username,
                    partnerTransId = transCodeToCheck,
                    transType = transType
                };
                var plainText =
                    $"partnerCode={data.partnerCode}&partnerTransId={data.partnerTransId}&transType={data.transType}";
                data.sign = Sign(plainText, providerInfo.PrivateKeyFile);
                string resCodeInfo = providerInfo.ExtraInfo;
                _logger.LogInformation("Trans Check object send: " + data.ToJson());
                var result = await CallApi(providerInfo.ApiUrl + "/check-transaction", data, providerInfo.Timeout);
                if (result != null)
                {
                    _logger.LogInformation(
                        $"{providerCode}-{transCodeToCheck}  WPayConnector Check trans return: {transCodeToCheck}-{transCode}-{result.ToJson()}");

                    if (result.resCode == "00")
                    {
                        if (result.status == 0)
                        {
                            responseMessage.ResponseCode = ResponseCodeConst.Success;
                            responseMessage.ResponseMessage = "Giao dịch thành công";
                            responseMessage.ProviderResponseTransCode = string.Empty;
                            responseMessage.ReceiverType = result.mobileType;
                            if (serviceCode.StartsWith("PIN"))
                                responseMessage.Payload = await GetCardRetrieveAsync(providerInfo, transCodeToCheck);
                        }
                        else if (result.status == 2)
                        {
                            responseMessage.ResponseCode = ResponseCodeConst.Error;
                            responseMessage.ResponseMessage = "Giao dịch thất bại";
                            responseMessage.ProviderResponseCode = result.status.ToString();
                            responseMessage.ProviderResponseMessage = "Giao dịch thất bại";
                        }
                        else
                        {
                            responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                            responseMessage.ResponseMessage = "Giao dịch Chưa có kết quá";
                            responseMessage.ProviderResponseCode = result.status.ToString();
                            responseMessage.ProviderResponseMessage = "Giao dịch chưa có kết quả";
                        }
                    }
                    // else if (resCodeInfo.Contains(result.resCode) && result.resCode != "05")
                    // {
                    //     // var reResult =
                    //     //     await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.WPAY, result.resCode,
                    //     //         transCodeToCheck);
                    //     responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    //     responseMessage.ResponseMessage = "Giao dịch chưa có kết quả";
                    //     responseMessage.ProviderResponseCode = result?.resCode;
                    //     responseMessage.ProviderResponseMessage = result?.resMessage;
                    // }
                    else
                    {
                        // var reResult =
                        //     await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.WPAY, result.resCode,
                        //         transCodeToCheck);
                        responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                        responseMessage.ResponseMessage = "Giao dịch chưa có kết quả";
                        responseMessage.ProviderResponseCode = result?.resCode;
                        responseMessage.ProviderResponseMessage = result?.resMessage;
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
            _logger.LogInformation($"{payBillRequestLog.TransCode} WPayConnector Query request: " +
                                   payBillRequestLog.ToJson());
            var responseMessage = new NewMessageResponseBase<InvoiceResultDto>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Truy vấn thông tin không thành công")
            };
            var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(payBillRequestLog.ProviderCode);

            if (providerInfo == null)
                return responseMessage;


            if (!_topupGatewayService.ValidConnector(ProviderConst.WPAY, providerInfo.ProviderCode))
            {
                _logger.LogError(
                    $"{payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{providerInfo.ProviderCode}-WPayConnector ProviderConnector not valid");
                responseMessage.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error,
                    "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ");
                return responseMessage;
            }


            var providerService =
                providerInfo.ProviderServices.Find(p => p.ProductCode == payBillRequestLog.ProductCode);
            var resCodeInfo = providerInfo.ExtraInfo;
            var telCo = string.Empty;
            var tranType = string.Empty;
            if (providerService != null)
            {
                telCo = providerService.ServiceCode.Split('|')[0];
                tranType = providerService.ServiceCode.Split('|')[1];
            }
            else
                _logger.LogWarning(
                    $"{payBillRequestLog.TransCode} ProviderService with ProductCode [{payBillRequestLog.ProductCode}] is null");


            var data = new WPayRequest
            {
                partnerCode = providerInfo.Username,
                productCode = telCo,
                billingCode = payBillRequestLog.ReceiverInfo,
            };

            var plainText =
                $"partnerCode={data.partnerCode}&productCode={data.productCode}&telcoCode={data.telcoCode}&billingCode={data.billingCode}";

            _logger.LogInformation("WPayConnector send: " + data.ToJson());
            var result = await CallApi(providerInfo.ApiUrl + "/query-bill", data, providerInfo.Timeout);

            if (result != null)
            {
                _logger.LogInformation(
                    $"{payBillRequestLog.ProviderCode}-{payBillRequestLog.TransCode} WPayConnector return: {payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{result.ToJson()}");
                if (result.resCode == "00")
                {
                    var dto = new InvoiceResultDto()
                    {
                        Amount = result.amount,
                        CustomerName = result.billingName,
                    };
                    responseMessage.ResponseStatus.ErrorCode = ResponseCodeConst.Success;
                    responseMessage.ResponseStatus.Message = "Giao dịch thành công";
                    responseMessage.Results = dto;
                }
                else if (resCodeInfo.Contains(result.resCode))
                {
                    var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.WPAY,
                        result.resCode,
                        payBillRequestLog.TransCode);
                    responseMessage.ResponseStatus.ErrorCode =
                        reResult != null ? reResult.ResponseCode : ResponseCodeConst.Error;
                    responseMessage.ResponseStatus.Message =
                        reResult != null ? reResult.ResponseName : "Giao dịch không thành công";
                }
                else
                {
                    var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.WPAY,
                        result.resCode,
                        payBillRequestLog.TransCode);
                    responseMessage.ResponseStatus.ErrorCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseStatus.Message =
                        reResult != null
                            ? reResult.ResponseName
                            : "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                }
            }
            else
            {
                _logger.LogInformation($"{payBillRequestLog.TransCode} Error send request");
                responseMessage.ResponseStatus.ErrorCode = ResponseCodeConst.Error;
                responseMessage.ResponseStatus.Message = "Lỗi kết nối nhà cung cấp";
            }

            return responseMessage;
        }

        public async Task<MessageResponseBase> CardGetByBatchAsync(CardRequestLogDto cardRequestLog)
        {
            _logger.LogInformation($"{cardRequestLog.TransCode} WPayConnector card request: " +
                                   cardRequestLog.ToJson());
            var responseMessage = new MessageResponseBase();
            var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(cardRequestLog.ProviderCode);

            if (providerInfo == null)
                return responseMessage;

            if (!_topupGatewayService.ValidConnector(ProviderConst.WPAY, providerInfo.ProviderCode))
            {
                _logger.LogError(
                    $"{cardRequestLog.TransCode}-{cardRequestLog.TransRef}-{providerInfo.ProviderCode}-WPayConnector ProviderConnector not valid");
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                };
            }

            var resCodeInfo = providerInfo.ExtraInfo;
            var str = cardRequestLog.ProductCode.Split('_');
            string keyCode = $"{str[0]}_{str[1]}";
            var providerService = providerInfo.ProviderServices.Find(p => p.ProductCode == keyCode);
            var telcoCode = string.Empty;

            if (providerService != null)
                telcoCode = providerService.ServiceCode.Split('|')[0];
            else
                _logger.LogInformation(
                    $"{cardRequestLog.TransCode}-{cardRequestLog.TransRef} ProviderService with ProductCode [{cardRequestLog.ProductCode}] is null");

            var data = new WPayRequest
            {
                partnerCode = providerInfo.Username,
                partnerTransId = cardRequestLog.TransCode,
                productCode = "",
                quantity = cardRequestLog.Quantity
            };

            data.productCode = telcoCode + (cardRequestLog.TransAmount / 1000).ToString().Split('.')[0].Split(',')[0];
            var plainText =
                $"partnerCode={data.partnerCode}&partnerTransId={data.partnerTransId}&productCode={data.productCode}&quantity={data.quantity}";
            data.sign = Sign(plainText, providerInfo.PrivateKeyFile);

            responseMessage.TransCodeProvider = cardRequestLog.TransCode;

            _logger.LogInformation($"{cardRequestLog.TransCode} WPayConnector send: " + data.ToJson());
            var result = await CallApi(providerInfo.ApiUrl + "/buy-card", data, providerInfo.Timeout);

            if (result != null)
            {
                decimal value = cardRequestLog.TransAmount;
                _logger.LogInformation(
                    $"WPAY Card return: {cardRequestLog.TransCode}-{cardRequestLog.TransRef}-{result.ToJson()}");
                cardRequestLog.ModifiedDate = DateTime.Now;
                cardRequestLog.ResponseInfo = result.ToJson();
                if (result.resCode == "00")
                {
                    cardRequestLog.ResponseInfo = "";
                    cardRequestLog.Status = TransRequestStatus.Success;
                    responseMessage.ResponseCode = ResponseCodeConst.Success;
                    responseMessage.ResponseMessage = "Giao dịch thành công";
                    responseMessage.Payload = DecryptCard(result.cardList, cardRequestLog.TransCode, value,
                        providerInfo.PrivateKeyFile);
                }
                else if (resCodeInfo.Contains(result.resCode))
                {
                    var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.WPAY,
                        result.resCode, cardRequestLog.TransCode);
                    cardRequestLog.Status = TransRequestStatus.Fail;
                    responseMessage.ResponseCode = reResult != null ? reResult.ResponseCode : ResponseCodeConst.Error;
                    responseMessage.ResponseMessage =
                        reResult != null ? reResult.ResponseName : "Giao dịch không thành công";
                }
                else
                {
                    var reResult =
                        await _topupGatewayService.GetResponseMassageCacheAsync("WPAY", result.resCode,
                            cardRequestLog.TransCode);
                    cardRequestLog.Status = TransRequestStatus.Timeout;
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage = reResult != null
                        ? reResult.ResponseName
                        : "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
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
            _logger.LogInformation($"{transCode} Get balance request: " + transCode);
            var responseMessage = new MessageResponseBase();
            var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(providerCode);

            if (providerInfo == null)
                return responseMessage;

            if (!_topupGatewayService.ValidConnector(ProviderConst.WPAY, providerInfo.ProviderCode))
            {
                _logger.LogError($"{providerCode}-{transCode}-WPayConnector ProviderConnector not valid");
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                };
            }

            var data = new WPayRequest
            {
                partnerCode = providerInfo.Username
            };

            var resCodeInfo = providerInfo.ExtraInfo;
            var plainText = $"partnerCode={data.partnerCode}";
            data.sign = Sign(plainText, providerInfo.PrivateKeyFile);
            _logger.LogInformation($"{transCode} Balance object send: " + data.ToJson());
            var result = await CallApi(providerInfo.ApiUrl + "/check-balance", data, providerInfo.Timeout);

            if (result != null)
            {
                _logger.LogInformation($"{transCode} Balance return: {transCode}-{result.ToJson()}");
                if (result.resCode == "00")
                {
                    responseMessage.ResponseCode = ResponseCodeConst.Success;
                    responseMessage.ResponseMessage = "Giao dịch thành công";
                    responseMessage.Payload = result.currentBalance.ToString();
                }
                else if (resCodeInfo.Contains(result.resCode))
                {
                    var reResult =
                        await _topupGatewayService.GetResponseMassageCacheAsync("WPAY", result.resCode, transCode);
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage =
                        reResult != null
                            ? reResult.ResponseName
                            : "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                }
                else
                {
                    var reResult =
                        await _topupGatewayService.GetResponseMassageCacheAsync("WPAY", result.resCode, transCode);
                    responseMessage.ResponseCode = reResult != null ? reResult.ResponseCode : ResponseCodeConst.Error;
                    responseMessage.ResponseMessage = reResult != null ? reResult.ResponseName : result.resMessage;
                }
            }
            else
            {
                _logger.LogInformation($"{transCode} Error send request");
                responseMessage.ResponseMessage = "Lỗi kết nối nhà cung cấp";
            }

            return responseMessage;
        }

        private async Task<List<CardRequestResponseDto>> GetCardRetrieveAsync(ProviderInfoDto providerInfo,
            string transCodeCheck)
        {
            var data = new WPayRequest
            {
                partnerCode = providerInfo.Username,
                partnerTransId = transCodeCheck,
            };

            var plainText = $"partnerCode={data.partnerCode}&partnerTransId={data.partnerTransId}";
            data.sign = Sign(plainText, providerInfo.PrivateKeyFile);
            var result = await CallApi(providerInfo.ApiUrl + "/retrieve-card", data, providerInfo.Timeout);

            if (result != null)
            {
                _logger.LogInformation(
                    $"TransCodeCheck= {transCodeCheck}|GetCardRetrieveAsync_Return: {result.ToJson()}");
                if (result.resCode == "00")
                {
                    var payload = DecryptCard(result.cardList, transCodeCheck, 0, providerInfo.PrivateKeyFile);
                    foreach (var item in payload)
                        item.CardCode = item.CardCode.EncryptTripDes();
                    return payload;
                }
            }

            return null;
        }

        public Task<MessageResponseBase> DepositAsync(DepositRequestDto request)
        {
            throw new NotImplementedException();
        }

        public async Task<MessageResponseBase> PayBillAsync(PayBillRequestLogDto payBillRequestLog)
        {
            _logger.LogInformation($"{payBillRequestLog.TransCode} WPayConnector Paybill request: " +
                                   payBillRequestLog.ToJson());
            var responseMessage = new MessageResponseBase();

            var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(payBillRequestLog.ProviderCode);

            if (providerInfo == null)
                return responseMessage;

            if (!_topupGatewayService.ValidConnector(ProviderConst.WPAY, providerInfo.ProviderCode))
            {
                _logger.LogError(
                    $"{payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{providerInfo.ProviderCode}-WPayConnector ProviderConnector not valid");
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                };
            }

            var providerService =
                providerInfo.ProviderServices.Find(p => p.ProductCode == payBillRequestLog.ProductCode);
            var resCodeInfo = providerInfo.ExtraInfo;
            var telCode = string.Empty;
            var transType = string.Empty;
            if (providerService != null)
            {
                telCode = providerService.ServiceCode.Split('|')[0];
                transType = providerService.ServiceCode.Split('|')[1];
            }
            else
                _logger.LogWarning(
                    $"{payBillRequestLog.TransCode} ProviderService with ProductCode [{payBillRequestLog.ProductCode}] is null");

            var data = new WPayRequest
            {
                partnerCode = providerInfo.Username,
                partnerTransId = payBillRequestLog.TransCode,
                productCode = telCode,
                billingCode = payBillRequestLog.ReceiverInfo,
                paidAmount = long.Parse(payBillRequestLog.TransAmount.ToString()),
            };

            var plainText =
                $"partnerCode={data.partnerCode}&partnerTransId={data.partnerTransId}&productCode={data.productCode}&billingCode={data.billingCode}&paidAmount={data.paidAmount}";
            data.sign = Sign(plainText, providerInfo.PrivateKeyFile);
            responseMessage.TransCodeProvider = payBillRequestLog.TransCode;
            _logger.LogInformation($"{payBillRequestLog.TransCode} WPayConnector send: " + data.ToJson());
            var result = await CallApi(providerInfo.ApiUrl + "/pay-bill", data, providerInfo.Timeout);
            _logger.LogInformation(
                $"CallApi return: {payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{result.ToJson()}");
            try
            {
                if (result.resCode == "00")
                {
                    payBillRequestLog.ModifiedDate = DateTime.Now;
                    payBillRequestLog.ResponseInfo = result.ToJson();
                    _logger.LogInformation(
                        $"WPayConnector return: {payBillRequestLog.ProviderCode}-{payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{result.ToJson()}");
                    payBillRequestLog.Status = TransRequestStatus.Success;
                    responseMessage.ResponseCode = ResponseCodeConst.Success;
                    responseMessage.ResponseMessage = "Thành công";
                }
                else if (resCodeInfo.Contains(result.resCode))
                {
                    payBillRequestLog.ModifiedDate = DateTime.Now;
                    payBillRequestLog.ResponseInfo = result.ToJson();
                    _logger.LogInformation(
                        $"WPayConnector return:{payBillRequestLog.ProviderCode}-{payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{result.ToJson()}");
                    payBillRequestLog.Status = TransRequestStatus.Fail;
                    responseMessage.ResponseCode = ResponseCodeConst.Error;
                    responseMessage.ResponseMessage = "Giao dịch không thành công từ nhà cung cấp";
                    var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.WPAY,
                        result.resCode, payBillRequestLog.TransCode);
                    responseMessage.ResponseCode = reResult != null ? reResult.ResponseCode : ResponseCodeConst.Error;
                    responseMessage.ResponseMessage = reResult != null
                        ? reResult.ResponseName
                        : "Giao dịch không thành công từ nhà cung cấp";
                }
                else
                {
                    _logger.LogInformation(
                        $"WPayConnector return:{payBillRequestLog.ProviderCode}-{payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{result.ToJson()}");
                    responseMessage.ResponseMessage =
                        "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_TimeOut;
                    payBillRequestLog.Status = TransRequestStatus.Timeout;
                    payBillRequestLog.ModifiedDate = DateTime.Now;
                }

                responseMessage.ProviderResponseCode = result?.resCode;
                responseMessage.ProviderResponseMessage = result?.resMessage;
            }
            catch (Exception ex)
            {
                _logger.LogInformation(
                    $"WPayConnector Error: {payBillRequestLog.ProviderCode}-{payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{result.ToJson()} Exception: {ex}");
                responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_TimeOut;
                responseMessage.Exception = ex.Message;
                payBillRequestLog.Status = TransRequestStatus.Timeout;
            }

            await _topupGatewayService.PayBillRequestLogUpdateAsync(payBillRequestLog);

            return responseMessage;
        }

        #region Private

        private async Task<WPayReponse> CallApi(string url, WPayRequest wPayRequest, int timeout)
        {
            string responseString = string.Empty;
            string exception = string.Empty;
            var retryCount = 0;
            do
            {
                try
                {
                    var clientHandler = new HttpClientHandler();
                    clientHandler.ServerCertificateCustomValidationCallback =
                        (sender, cert, chain, sslPolicyErrors) => true;

                    responseString =
                        await url.PostStringToUrlAsync(wPayRequest.ToJson(), contentType: "application/json");
                    _logger.LogInformation($"WPAY callapi response {wPayRequest.partnerTransId}-{responseString}");
                    retryCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError("Trans exception: " + ex.Message);
                    exception = ex.Message;
                    responseString = "TIMEOUT";
                }
            } while (string.IsNullOrEmpty(responseString) && retryCount < 3);

            if (!string.IsNullOrEmpty(responseString))
            {
                if (responseString == "TIMEOUT")
                {
                    return new WPayReponse()
                    {
                        resCode = "501102",
                        resMessage = exception
                    };
                }

                var responseMessage = responseString.FromJson<WPayReponse>();
                return responseMessage;
            }

            return new WPayReponse()
            {
                resCode = "501102",
                resMessage = "Send request timeout!"
            };
        }

        private string Sign(string dataToSign, string privateFile)
        {
            var privateKeyText = File.ReadAllText("files/" + privateFile);
            var privateKeyBlocks = privateKeyText.Split("-", StringSplitOptions.RemoveEmptyEntries);
            string str = privateKeyBlocks[1].Replace("\r\n", "");
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

        private List<CardRequestResponseDto> DecryptCard(List<WPayCardInfo> list, string transCode, decimal amount,
            string privateFile)
        {
            try
            {
                var privateKeyText = File.ReadAllText("files/" + privateFile);
                var privateKeyBlocks = privateKeyText.Split("-", StringSplitOptions.RemoveEmptyEntries);
                string str = privateKeyBlocks[1].Replace("\r\n", "");
                var privateKeyBytes = Convert.FromBase64String(str);

                using var rsa = RSA.Create();

                if (privateKeyBlocks[0] == "BEGIN PRIVATE KEY")
                    rsa.ImportPkcs8PrivateKey(privateKeyBytes, out _);
                else if (privateKeyBlocks[0] == "BEGIN RSA PRIVATE KEY")
                    rsa.ImportRSAPrivateKey(privateKeyBytes, out _);

                var cardList = new List<CardRequestResponseDto>();
                foreach (var item in list)
                {
                    var cardItem = new CardRequestResponseDto
                    {
                        CardType = "",
                        CardValue = amount.ToString(),
                        CardCode = Decrypt(item.pin_code, rsa),
                        Serial = item.serial_code,
                    };

                    cardItem.ExpireDate = item.expire_date;
                    if (!string.IsNullOrEmpty(item.expire_date))
                    {
                        //31-12-2028
                        //var k = cardItem.ExpireDate.Split('-');
                        //cardItem.ExpiredDate = new DateTime(Convert.ToInt32(k[2]), Convert.ToInt32(k[1]),Convert.ToInt32(k[0]));
                        cardItem.ExpiredDate = DateTime.ParseExact(cardItem.ExpireDate, "dd-MM-yyyy",
                            CultureInfo.InvariantCulture);
                        cardItem.ExpireDate = cardItem.ExpiredDate.ToString("dd/MM/yyyy");
                    }

                    cardList.Add(cardItem);
                }

                return cardList;
            }
            catch (Exception e)
            {
                _logger.LogError($"{transCode} Error decrypt cards: " + e.Message);
                return new List<CardRequestResponseDto>();
            }
        }

        private string Decrypt(string decryptedData, RSA rsa)
        {
            var passwordByte = Encoding.UTF8.GetBytes(decryptedData);
            var keySize = 128; // rsaPrivateKey.Modulus.Length;
            var base64BlockSize = keySize % 3 != 0 ? keySize / 3 * 4 + 4 : keySize / 3 * 4;
            var dataLength = passwordByte.Length;
            var iterations = dataLength / base64BlockSize;
            var listByte = new List<byte>();
            for (var i = 0; i < iterations; ++i)
            {
                var sTemp = decryptedData.Substring(base64BlockSize * i, base64BlockSize * i + base64BlockSize);
                var bTemp = Convert.FromBase64String(sTemp);
                var encryptedBytes = rsa.Decrypt(bTemp, RSAEncryptionPadding.Pkcs1);
                listByte.AddRange(encryptedBytes);
            }

            var decrypted = Encoding.UTF8.GetString(listByte.ToArray());

            return decrypted;
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