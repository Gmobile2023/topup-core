﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Topup.Shared;
using Topup.Shared.Dtos;
using Topup.Shared.Utils;
using Topup.TopupGw.Contacts.ApiRequests;
using Topup.TopupGw.Contacts.Dtos;
using Topup.TopupGw.Contacts.Enums;
using Topup.TopupGw.Domains.BusinessServices;
using Microsoft.Extensions.Logging;
using ServiceStack;
using Topup.Shared.Helpers;


namespace Topup.TopupGw.Components.Connectors.VDS
{
    public class VDSConnector : IGatewayConnector
    {
        private const string VDS_SOAP =
            "<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\" xmlns:par=\"http://partnerapi.bankplus.viettel.com/\"><soapenv:Header/><soapenv:Body><par:process><cmd>{0}</cmd><data>{1}</data><signature>{2}</signature></par:process></soapenv:Body></soapenv:Envelope>";

        private readonly ITopupGatewayService _topupGatewayService;
        private readonly ILogger<VDSConnector> _logger;
        private IDateTimeHelper _dateTimeHelper;

        public VDSConnector(ITopupGatewayService topupGatewayService, ILogger<VDSConnector> logger,
            IDateTimeHelper dateTimeHelper)
        {
            _topupGatewayService = topupGatewayService;
            _logger = logger;
            _dateTimeHelper = dateTimeHelper;
        }

        public async Task<MessageResponseBase> TopupAsync(TopupRequestLogDto topupRequestLog,
            ProviderInfoDto providerInfo)
        {
            _logger.LogInformation($"{topupRequestLog.TransCode} VDSConnector topup request: " +
                                   topupRequestLog.ToJson());
            var responseMessage = new MessageResponseBase();
            try
            {
                if (!_topupGatewayService.ValidConnector(ProviderConst.VDS, providerInfo.ProviderCode))
                {
                    _logger.LogError(
                        $"{topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{providerInfo.ProviderCode}-VDSConnector ProviderConnector not valid");
                    return new MessageResponseBase
                    {
                        ResponseCode = ResponseCodeConst.Error,
                        ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                    };
                }

                var encryptedPassword = string.IsNullOrEmpty(providerInfo.ApiPassword)
                    ? Encrypt(providerInfo.Password, providerInfo.PublicKey)
                    : providerInfo.ApiPassword;
                var requestDate = _dateTimeHelper.ConvertToUserTime(topupRequestLog.RequestDate, DateTimeKind.Utc);
                var transDate = requestDate.ToString("yyyyMMddHHmmss");
                var data = new DataObject
                {
                    OrderId = topupRequestLog.TransCode,
                    Username = providerInfo.Username,
                    Password = encryptedPassword,
                    ServiceCode = "100000",
                    Amount = topupRequestLog.TransAmount,
                    TransDate = transDate,
                    ChannelInfo = new ChannelInfo
                    {
                        ChannelType = "website",
                        WebsiteName = "shop",
                        WebsiteAddress = "shop",
                        Source = "internetBanking",
                        AccNo = "", //"1201100053003",
                        BankCode = "" //"MB"
                    },
                    BillingCode = topupRequestLog.ReceiverInfo.StartsWith("0")
                        ? topupRequestLog.ReceiverInfo.Substring(1, topupRequestLog.ReceiverInfo.Length - 1)
                        : topupRequestLog.ReceiverInfo,
                };
                responseMessage.TransCodeProvider = topupRequestLog.TransCode;
                var json = data.ToJson();
                var cmd = new VDSRequest
                {
                    Cmd = "PAY_TELECHARGE_VT",
                    Data = data,
                    Signature = Sign(json, providerInfo.PrivateKeyFile)
                };
                var result = await CallApi(providerInfo.ApiUrl, cmd, providerInfo.Timeout);
                if (result != null)
                {
                    topupRequestLog.ResponseInfo = result.Data.ToJson();
                    topupRequestLog.ModifiedDate = DateTime.Now;
                    responseMessage.ProviderResponseCode = result?.Data.ErrorCode;
                    responseMessage.ProviderResponseMessage = result?.Data.ErrorMsg;

                    //_logger.LogInformation($"{topupRequestLog.ProviderCode}-{topupRequestLog.TransCode} ViettelVtt2Connector return: {topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{result.Data.ToJson()}");
                    if (result.Data.ErrorCode == "00")
                    {
                        topupRequestLog.Status = TransRequestStatus.Success;
                        responseMessage.ResponseCode = ResponseCodeConst.Success;
                        responseMessage.ResponseMessage = "Giao dịch thành công";
                        responseMessage.ProviderResponseTransCode = result.Data.TransId;
                        responseMessage.ReceiverType = result.Data.TppType;
                    }
                    else if (new[] { "32", "232", "233", "605", "K02", "650", "501102" }
                             .Contains(result.Data.ErrorCode))
                    {
                        // var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.VDS,
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
                                await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.VDS, errorCode,
                                    topupRequestLog.TransCode);
                            if (reResult == null && errorCode != result.Data.ErrorCode)
                                reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.VDS,
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
            _logger.LogInformation($"{transCodeToCheck} VDSConnector check request: " + transCodeToCheck + "|" +
                                   transCode);
            var responseMessage = new MessageResponseBase();
            try
            {
                if (providerInfo == null)
                    providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(providerCode);


                if (providerInfo == null ||
                    !_topupGatewayService.ValidConnector(ProviderConst.VDS, providerInfo.ProviderCode))
                {
                    _logger.LogError($"{transCode}-{providerCode}-VDSConnector ProviderConnector not valid");
                    return new MessageResponseBase
                    {
                        ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult,
                        ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ"
                    };
                }
                var topupRequestLog = await _topupGatewayService.TopupRequestLogGetAsync(providerCode, transCodeToCheck);
                if (topupRequestLog == null)
                {
                    _logger.LogError($"{transCode}-{providerCode}-VDSConnector get transaction not found");
                    return new MessageResponseBase
                    {
                        ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult,
                        ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ"
                    };
                }
                var encryptedPassword = string.IsNullOrEmpty(providerInfo.ApiPassword)
                    ? Encrypt(providerInfo.Password, providerInfo.PublicKey)
                    : providerInfo.ApiPassword;
                var requestDate = _dateTimeHelper.ConvertToUserTime(topupRequestLog.RequestDate, DateTimeKind.Utc);
                var data = new DataObject
                {
                    OrderId = transCode,
                    OriginalOrderId = transCodeToCheck,
                    Username = providerInfo.Username,
                    Password = encryptedPassword,
                    ServiceCode = "100000",
                    TransDate = DateTime.Now.ToString("yyyyMMddHHmmss"),
                    OriginalRequestDate = requestDate.ToString("yyyyMMddHHmmss")
                };
                var json = data.ToJson();

                var cmd = new VDSRequest
                {
                    Cmd = "CHECK_TRANSACTION",
                    Data = data,
                    Signature = Sign(json, providerInfo.PrivateKeyFile)
                };

                //_logger.LogInformation("Trans Check object send: " + Cmd.ToJson());
                var result = await CallApi(providerInfo.ApiUrl, cmd, providerInfo.Timeout);
                if (result != null)
                {
                    _logger.LogInformation(
                        $"{providerCode}-{transCodeToCheck} VDSConnector Check trans return: {transCodeToCheck}-{transCode}-{result.Data.ToJson()}");
                    if (result.Data.ErrorCode == "00")
                    {
                        if (result.Data.ReferenceCode == "00")
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
                            //     await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.VDS,
                            //         result.Data.ReferenceCode,
                            //         transCodeToCheck);
                            responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                            responseMessage.ResponseMessage = "Giao dịch chưa có kết quả từ NCC";
                            responseMessage.ProviderResponseCode = result?.Data.ReferenceCode;
                            responseMessage.ProviderResponseMessage = result?.Data.ReferenceMessage;
                        }
                        else
                        {
                            var reResult =
                                await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.VDS,
                                    result.Data.ReferenceCode,
                                    transCodeToCheck);
                            responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_ErrorProvider;
                            responseMessage.ResponseMessage =
                                reResult != null ? reResult.ResponseName : "Giao dịch lỗi phía NCC";
                            responseMessage.ProviderResponseCode = result?.Data.ReferenceCode;
                            responseMessage.ProviderResponseMessage = result?.Data.ReferenceMessage;
                        }
                    }
                    else
                    {
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
            _logger.LogInformation($"{payBillRequestLog.TransCode} VDSConnector Query request: " +
                                   payBillRequestLog.ToJson());
            var responseMessage = new NewMessageResponseBase<InvoiceResultDto>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Truy vấn thông tin không thành công")
            };
            var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(payBillRequestLog.ProviderCode);

            if (providerInfo == null)
                return responseMessage;


            if (!_topupGatewayService.ValidConnector(ProviderConst.VDS, providerInfo.ProviderCode))
            {
                _logger.LogError(
                    $"{payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{providerInfo.ProviderCode}-VDSConnector ProviderConnector not valid");
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

            var cmd = new VDSRequest
            {
                Cmd = "GET_TELECHARGE_VT_INFO",
                Data = data,
                Signature = Sign(data.ToJson(), providerInfo.PrivateKeyFile) //.SplitFixedLength(76).Join("\n")
            };

            var result = await CallApi(providerInfo.ApiUrl, cmd, providerInfo.Timeout);
            if (result != null)
            {
                _logger.LogInformation(
                    $"{payBillRequestLog.ProviderCode}-{payBillRequestLog.TransCode} VDSConnector return: {payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{result.Data.ToJson()}");
                if (result.Data.ErrorCode == "00")
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
                else if (new[] { "32", "232", "233", "605", "K02", "650", "501102" }.Contains(result.Data.ErrorCode))
                {
                    var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.VDS,
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
                    var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.VDS,
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
            _logger.LogInformation($"{cardRequestLog.TransCode} VDSConnector card request: " +
                                   cardRequestLog.ToJson());
            var responseMessage = new MessageResponseBase();
            var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(cardRequestLog.ProviderCode);

            if (providerInfo == null)
                return responseMessage;

            if (!_topupGatewayService.ValidConnector(ProviderConst.VDS, providerInfo.ProviderCode))
            {
                _logger.LogError(
                    $"{cardRequestLog.TransCode}-{cardRequestLog.TransRef}-{providerInfo.ProviderCode}-VDSConnector ProviderConnector not valid");
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
                ServiceCode = "PCEBATCH",
                Amount = (int)cardRequestLog.TransAmount,
                Quantity = cardRequestLog.Quantity,
                PayerMsisdn = providerInfo.ApiUser
            };
            responseMessage.TransCodeProvider = cardRequestLog.TransCode;
            var json = data.ToJson();
            var cmd = new VDSCardRequest
            {
                Cmd = "PAY_PINCODE_VT_BATCH",
                Data = data,
                Signature = Sign(json, providerInfo.PrivateKeyFile)
            };

            var result = await CallCardApi(providerInfo.ApiUrl, cmd, providerInfo.Timeout);
            if (result != null)
            {
                _logger.LogInformation(
                    $"Card return: {cardRequestLog.TransCode}-{cardRequestLog.TransRef}-{result.Data.ToJson()}");
                cardRequestLog.ModifiedDate = DateTime.Now;
                cardRequestLog.ResponseInfo = result.Data.ToJson();
                if (result.Data.ErrorCode == "00")
                {
                    cardRequestLog.Status = TransRequestStatus.Success;
                    responseMessage.ResponseCode = ResponseCodeConst.Success;
                    responseMessage.ResponseMessage = "Giao dịch thành công";
                    responseMessage.ProviderResponseTransCode = result.Data.TransId;
                    try
                    {
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

                        var viettelCards = addStr.FromJson<List<VDSCards>>();
                        var cardList = viettelCards.Select(viettelCard => new CardRequestResponseDto
                        {
                            CardType = "VTE",
                            CardValue = viettelCard.Amount,
                            CardCode = viettelCard.Pincode,
                            Serial = viettelCard.Serial,
                            ExpireDate = DateTime.ParseExact(viettelCard.Expire, "yyyy-MM-ddTHH:mm:ss",
                                    CultureInfo.InvariantCulture).ToString("dd/MM/yyyy"),
                            ExpiredDate = DateTime.ParseExact(viettelCard.Expire, "yyyy-MM-ddTHH:mm:ss",
                                    CultureInfo.InvariantCulture)
                        })
                            .ToList();

                        responseMessage.Payload = cardList;
                    }
                    catch (Exception e)
                    {
                        _logger.LogError($"{cardRequestLog.TransCode} Error decrypt cards: " + e.Message);
                    }
                }
                else if (new[] { "32", "232", "233", "605", "K02", "650", "501102" }.Contains(result.Data.ErrorCode))
                {
                    var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.VDS,
                        result.Data.ErrorCode,
                        cardRequestLog.TransCode);
                    cardRequestLog.Status = TransRequestStatus.Timeout;
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage =
                        reResult != null
                            ? reResult.ResponseName
                            : "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                }
                else
                {
                    var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.VDS,
                        result.Data.ErrorCode,
                        cardRequestLog.TransCode);
                    cardRequestLog.Status = TransRequestStatus.Fail;
                    responseMessage.ResponseCode = reResult != null
                        ? reResult.ResponseCode
                        : ResponseCodeConst.ResponseCode_ErrorProvider;
                    responseMessage.ResponseMessage =
                        reResult != null ? reResult.ResponseName : "Giao dịch lỗi phía NCC";
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
            _logger.LogInformation($"{transCode} VDSConnector_balance request");
            var responseMessage = new MessageResponseBase();
            try
            {
                var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(providerCode);

                if (providerInfo == null)
                    return responseMessage;

                if (!_topupGatewayService.ValidConnector(ProviderConst.VDS, providerInfo.ProviderCode))
                {
                    _logger.LogError($"{providerCode}-{transCode}-VDSConnector ProviderConnector not valid");
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
                var cmd = new VDSRequest
                {
                    Cmd = "CHECK_CP",
                    Data = data,
                    Signature = Sign(json, providerInfo.PrivateKeyFile)
                };
                var result = await CallApi(providerInfo.ApiUrl, cmd);
                _logger.LogInformation($"{transCode} VDSConnector_balance return:{result.ToJson()}");
                if (result != null)
                {
                    responseMessage.ProviderResponseCode = result.Data.ErrorCode;
                    responseMessage.ProviderResponseMessage = result.Data.ErrorMsg;
                    if (result.Data.ErrorCode == "00")
                    {
                        responseMessage.ResponseCode = ResponseCodeConst.Success;
                        responseMessage.ResponseMessage = "Giao dịch thành công";
                        responseMessage.Payload = result.Data.Balance;
                    }
                    else if (new[] { "32", "232", "233", "605", "K02", "650", "501102" }
                             .Contains(result.Data.ErrorCode))
                    {
                        var reResult =
                            await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.VDS,
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
                            await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.VDS,
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
                _logger.LogInformation($"{transCode} VDSConnector checkbalance error:{e}");
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

            if (!_topupGatewayService.ValidConnector(ProviderConst.VDS, providerInfo.ProviderCode))
            {
                _logger.LogError(
                    $"{request.TransCode}-{request.Amount}-{providerInfo.ProviderCode}-VDSConnector ProviderConnector not valid");
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

            var cmd = new VDSRequest
            {
                Cmd = "PAY_IN_PREPAID",
                Data = data,
                Signature = Sign(data.ToJson(), providerInfo.PrivateKeyFile)
            };

            var result = await CallApi(providerInfo.ApiUrl, cmd,
                providerInfo.Timeout);

            if (result != null)
            {
                _logger.LogInformation($"Deposit return: {request.TransCode}-{result.ToJson()}");
                responseMessage.ProviderResponseCode = result.Data.ErrorCode;
                responseMessage.ProviderResponseMessage = result.Data.ErrorMsg;
                if (result.Data.ErrorCode == "00")
                {
                    responseMessage.ResponseCode = ResponseCodeConst.Success;
                    responseMessage.ResponseMessage = "Giao dịch thành công";
                    responseMessage.Payload = result.Data.Balance;
                }
                else if (new[] { "32", "232", "233", "605", "K02", "650", "501102" }.Contains(result.Data.ErrorCode))
                {
                    var reResult =
                        await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.VDS,
                            result.Data.ErrorCode,
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
                        await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.VDS,
                            result.Data.ErrorCode,
                            request.TransCode);
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_ErrorProvider;
                    responseMessage.ResponseMessage =
                        reResult != null ? reResult.ResponseName : "Giao dịch lỗi phía NCC";
                }
            }
            else
            {
                _logger.LogInformation("Error send request");
                responseMessage.ResponseMessage = "Lỗi kết nối nhà cung cấp";
            }

            return responseMessage;
        }

        public Task<MessageResponseBase> PayBillAsync(PayBillRequestLogDto payBillRequestLog)
        {
            _logger.LogInformation("Get paybill request: ======== NOT IMPLEMENTED ====== /n" +
                                   payBillRequestLog.ToJson());
            throw new NotImplementedException();
        }

        private async Task<List<CardRequestResponseDto>> GetPinCodeAsync(ProviderInfoDto providerInfo,
            string providerCode,
            string transCodeToCheck, string transCode)
        {
            _logger.LogInformation($"{transCodeToCheck} VDSConnector GetPinCodeAsync request: " + transCodeToCheck +
                                   "|" +
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

            var cmd = new VDSRequest
            {
                Cmd = "RESEND_PINCODE_VT_BATCH",
                Data = data,
                Signature = Sign(json, providerInfo.PrivateKeyFile)
            };

            //_logger.LogInformation("Trans Check object send: " + Cmd.ToJson());
            var result = await CallApi(providerInfo.ApiUrl, cmd, providerInfo.Timeout);
            if (result != null)
            {
                _logger.LogInformation(
                    $"{providerCode}-{transCodeToCheck} VDSConnector GetPinCodeAsync return: {transCodeToCheck}-{transCode}-{result.Data.ToJson()}");
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

                            var viettelCards = addStr.FromJson<List<VDSCards>>();
                            foreach (var viettelCard in viettelCards)
                            {
                                cardList.Add(new CardRequestResponseDto
                                {
                                    CardType = "VTE",
                                    CardValue = viettelCard.Amount,
                                    CardCode = viettelCard.Pincode.EncryptTripDes(),
                                    Serial = viettelCard.Serial,
                                    ExpireDate = DateTime.ParseExact(viettelCard.Expire, "yyyy-MM-ddTHH:mm:ss",
                                        CultureInfo.InvariantCulture).ToString("dd/MM/yyyy"),
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

        private async Task<VDSResponse> CallApi(string url, VDSRequest request, int timeout = 120)
        {
            var envelop = CreateSoapEnvelope(request);
            var exeption = string.Empty;
            var responseString = string.Empty;
            var retryCount = 0;
            do
            {
                try
                {
                    var xml = envelop.InnerXml;
                    responseString = await url.PostStringToUrlAsync(xml, contentType: "text/xml");
                    responseString = responseString.Split("<return>")[1].Split("</return>")[0];
                    retryCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        $"TransCode= {request.Data.OrderId}|bindingCode ={request.Data.BillingCode}|Trans exception: " +
                        ex.Message);
                    exeption = ex.Message;
                    responseString = "TIMEOUT";
                }
            } while (string.IsNullOrEmpty(responseString) && retryCount < 3);

            if (!string.IsNullOrEmpty(responseString))
            {
                if (responseString == "TIMEOUT")
                    return new VDSResponse
                    {
                        Data = new DataObject
                        {
                            ErrorCode = "501102", //Định nghĩa mã lỗi cho trường hợp gọi Service timeout
                            ErrorMsg = exeption
                        }
                    };
                responseString = responseString.Replace("&quot;", "\"");
                var responseMessage = responseString.FromJson<VDSResponse>();
                return responseMessage;
            }

            return null;
        }

        private async Task<VDSResponse> CallCardApi(string url, VDSCardRequest request, int timeout = 120)
        {
            var envelop = CreateSoapEnvelope(request);
            var exeption = string.Empty;
            var responseString = string.Empty;
            var retryCount = 0;
            do
            {
                try
                {
                    var xml = envelop.InnerXml;
                    responseString = await url.PostStringToUrlAsync(xml, contentType: "text/xml");
                    responseString = responseString.Split("<return>")[1].Split("</return>")[0];
                    retryCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"TransCode= {request.Data.OrderId}|Trans exception: " + ex.Message);
                    exeption = ex.Message;
                    responseString = "TIMEOUT";
                }
            } while (string.IsNullOrEmpty(responseString) && retryCount < 3);

            if (!string.IsNullOrEmpty(responseString))
            {
                if (responseString == "TIMEOUT")
                    return new VDSResponse
                    {
                        Data = new DataObject
                        {
                            ErrorCode = "501102", //Định nghĩa mã lỗi cho trường hợp gọi Service timeout
                            ErrorMsg = exeption
                        }
                    };
                responseString = responseString.Replace("&quot;", "\"");
                var responseMessage = responseString.FromJson<VDSResponse>();
                return responseMessage;
            }

            return null;
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

        private static XmlDocument CreateSoapEnvelope(VDSRequest request)
        {
            var soapEnvelopeDocument = new XmlDocument();
            soapEnvelopeDocument.LoadXml(string.Format(VDS_SOAP, request.Cmd, request.Data.ToJson(),
                request.Signature));
            return soapEnvelopeDocument;
        }

        private static XmlDocument CreateSoapEnvelope(VDSCardRequest request)
        {
            var soapEnvelopeDocument = new XmlDocument();
            soapEnvelopeDocument.LoadXml(string.Format(VDS_SOAP, request.Cmd, request.Data.ToJson(),
                request.Signature));
            return soapEnvelopeDocument;
        }

        #endregion
    }
}