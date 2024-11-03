using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.Dtos;
using HLS.Paygate.TopupGw.Contacts.ApiRequests;
using HLS.Paygate.TopupGw.Contacts.Dtos;
using HLS.Paygate.TopupGw.Contacts.Enums;
using HLS.Paygate.TopupGw.Domains.BusinessServices;
using MassTransit;
using Microsoft.Extensions.Logging;
using ServiceStack;

namespace HLS.Paygate.TopupGw.Components.Connectors.Viettel;

public class ViettelVttConnector : IGatewayConnector
{
    private const string VIETTEL_SOAP =
        "<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\" xmlns:par=\"http://partnerapi.bankplus.viettel.com/\"><soapenv:Header/><soapenv:Body><par:process><cmd>{0}</cmd><data>{1}</data><signature>{2}</signature></par:process></soapenv:Body></soapenv:Envelope>";

    private readonly IBusControl _bus;

    private readonly ILogger<ViettelVttConnector> _logger; // = LogManager.GetLogger("ViettelVttConnector");

    private readonly ITopupGatewayService _topupGatewayService;

    public ViettelVttConnector(ITopupGatewayService topupGatewayService, ILogger<ViettelVttConnector> logger,
        IBusControl bus)
    {
        _topupGatewayService = topupGatewayService;
        _logger = logger;
        _bus = bus;
    }

    public async Task<MessageResponseBase> TopupAsync(TopupRequestLogDto topupRequestLog, ProviderInfoDto providerInfo)
    {
        _logger.LogInformation($"{topupRequestLog.TransCode} ViettelVttConnector topup request: " +
                               topupRequestLog.ToJson());
        var responseMessage = new MessageResponseBase();
        if (!_topupGatewayService.ValidConnector(ProviderConst.VTT, providerInfo.ProviderCode))
        {
            _logger.LogError(
                $"{topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{providerInfo.ProviderCode}-ViettelVttConnector ProviderConnector not valid");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
            };
        }


        var encryptedPassword = Encrypt(providerInfo.Password, providerInfo.PublicKey); //"changemeplease123a@");

        var data = new DataObject
        {
            Amount = topupRequestLog.TransAmount,
            OrderId = topupRequestLog.TransCode,
            ChannelInfo = new ChannelInfo
            {
                ChannelType = "website",
                WebsiteName = "MobileGo",
                WebsiteAddress = "mobilego.vn",
                Source = "internetBanking",
                AccNo = providerInfo.ExtraInfo.Split('|')[1], //"1201100053003",
                BankCode = providerInfo.ExtraInfo.Split('|')[0] //"MB"
            },
            PayerMsisdn = providerInfo.ApiUser, // "84983103127",
            ServiceCode = "100000",
            BillingCode = topupRequestLog.ReceiverInfo,
            Username = providerInfo.Username, //"partnerchain",
            Password = encryptedPassword //Encrypt("changemeplease123a@")
        }.ToJson();
        responseMessage.TransCodeProvider = topupRequestLog.TransCode;

        var command = new ViettelPayRequest
        {
            Command = "PAY_TELECHARGE_VT",
            Data = data,
            Sign = Sign(data, providerInfo.PrivateKeyFile)
        };

        _logger.LogInformation($"{topupRequestLog.TransCode} ViettelConnector send: " + command.ToJson());
        var result = await CallApi(providerInfo.ApiUrl, providerInfo.Timeout, command);

        if (result != null)
        {
            topupRequestLog.ResponseInfo = result.Data.ToJson();
            topupRequestLog.ModifiedDate = DateTime.Now;
            responseMessage.ProviderResponseCode = result?.Data.ErrorCode;
            responseMessage.ProviderResponseMessage = result?.Data.ErrorMsg;
            _logger.LogInformation(
                $"{topupRequestLog.ProviderCode}{topupRequestLog.TransCode} ViettelVttConnector return: {topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{result.Data.ToJson()}");
            if (result.Data.ErrorCode == "00")
            {
                topupRequestLog.Status = TransRequestStatus.Success;

                responseMessage.ResponseCode = ResponseCodeConst.Success;
                responseMessage.ResponseMessage = "Giao dịch thành công";
                responseMessage.ProviderResponseTransCode = result.Data.TransId;
                responseMessage.ReceiverType = result.Data.TppType;
            }
            else if (new[] { "32", "232", "233", "605", "K02", "650", "501102" }.Contains(result.Data.ErrorCode))
            {
                // var reResult = await _topupGatewayService.GetResponseMassageCacheAsync("VIETTEL", result.Data.ErrorCode,
                //     topupRequestLog.TransCode);
                topupRequestLog.Status = TransRequestStatus.Timeout;
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                responseMessage.ResponseMessage ="Giao dịch đang chờ kết quả xử lý.";
            }
            else
            {
                var reResult = await _topupGatewayService.GetResponseMassageCacheAsync("VIETTEL", result.Data.ErrorCode,
                    topupRequestLog.TransCode);
                topupRequestLog.Status = TransRequestStatus.Fail;
                responseMessage.ResponseCode = reResult != null ? reResult.ReponseCode : ResponseCodeConst.Error;
                responseMessage.ResponseMessage = reResult != null ? reResult.ReponseName : result.Data.ErrorMsg;
            }
        }
        else
        {
            _logger.LogInformation($"{topupRequestLog.TransCode} Error send request");
            responseMessage.ResponseMessage = "Lỗi kết nối nhà cung cấp";
            topupRequestLog.Status = TransRequestStatus.Fail;
        }

        await _topupGatewayService.TopupRequestLogUpdateAsync(topupRequestLog);

        return responseMessage;
    }

    public async Task<MessageResponseBase> TransactionCheckAsync(string providerCode, string transCodeToCheck,
        string transCode, string serviceCode = null, ProviderInfoDto providerInfo = null)
    {
        _logger.LogInformation($"{transCodeToCheck} ViettelVttConnector check request: " + transCodeToCheck + "|" +
                               transCode);
        var responseMessage = new MessageResponseBase();
        if (providerInfo == null)
            providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(providerCode);


        if (providerInfo == null ||
            !_topupGatewayService.ValidConnector(ProviderConst.VTT, providerInfo.ProviderCode))
        {
            _logger.LogError($"{transCode}-{providerCode}-ViettelVttConnector ProviderConnector not valid");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult,
                ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ"
            };
        }

        var encryptedPassword = Encrypt(providerInfo.Password, providerInfo.PublicKey); //"changemeplease123a@");
        var data = new DataObject
        {
            OrderId = transCode,
            ServiceCode = "100000",
            OriginalOrderId = transCodeToCheck,
            Username = providerInfo.Username, // "partnerchain",
            Password = encryptedPassword //Encrypt("changemeplease123a@")
        }.ToJson();

        var command = new ViettelPayRequest
        {
            Command = "CHECK_TRANSACTION",
            Data = data,
            Sign = Sign(data, providerInfo.PrivateKeyFile)
        };
        _logger.LogInformation("Trans Check object send: " + command.ToJson());
        var result = await CallApi(providerInfo.ApiUrl, providerInfo.Timeout, command);
        if (result != null)
        {
            _logger.LogInformation(
                $"{providerCode}-{transCodeToCheck} ViettelVttConnector Check trans return: {transCodeToCheck}-{transCode}-{result.ToJson()}");
            if (result.Data.ErrorCode == "00")
            {
                //responseMessage.ExtraInfo = string.Join("|", result.Data.ReferenceCode, result.Data.ReferenceMessage);
                if (result.Data.ReferenceCode == "00")
                {
                    responseMessage.ResponseCode = ResponseCodeConst.Success;
                    responseMessage.ResponseMessage = "Giao dịch thành công";
                    responseMessage.ProviderResponseTransCode = result.Data.TransId;
                    responseMessage.ReceiverType = result.Data.TppType;
                }
                else if (new[] { "32", "232", "233", "605", "K02", "650" }.Contains(result.Data.ReferenceCode))
                {
                    // var reResult =
                    //     await _topupGatewayService.GetResponseMassageCacheAsync("VIETTEL", result.Data.ReferenceCode,
                    //         transCodeToCheck);
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage ="Giao dịch chưa có kết quả";
                    responseMessage.ProviderResponseCode = result?.Data.ReferenceCode;
                    responseMessage.ProviderResponseMessage = result?.Data.ReferenceMessage;
                }
                else
                {
                    // var reResult =
                    //     await _topupGatewayService.GetResponseMassageCacheAsync("VIETTEL", result.Data.ReferenceCode,
                    //         transCodeToCheck);
                    responseMessage.ResponseCode = ResponseCodeConst.Error;
                    responseMessage.ResponseMessage ="Giao dịch không thành công từ nhà cung cấp";
                    responseMessage.ProviderResponseCode = result?.Data.ReferenceCode;
                    responseMessage.ProviderResponseMessage = result?.Data.ReferenceMessage;
                }
            }
            else if (new[] { "32", "232", "233", "605", "K02", "650", "501102" }.Contains(result.Data.ErrorCode))
            {
                //responseMessage.ExtraInfo = string.Join("|", result.Data.ErrorCode, result.Data.ErrorMsg);
                var reResult =
                    await _topupGatewayService.GetResponseMassageCacheAsync("VIETTEL", result.Data.ErrorCode,
                        transCodeToCheck);
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                responseMessage.ResponseMessage =
                    reResult != null
                        ? reResult.ReponseName
                        : "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                responseMessage.ProviderResponseCode = result?.Data.ErrorCode;
                responseMessage.ProviderResponseMessage = result?.Data.ErrorMsg;
            }
            else
            {
                // var reResult = await _topupGatewayService.GetResponseMassageCacheAsync("VIETTEL", result.Data.ErrorCode,
                //     transCodeToCheck);
                responseMessage.ResponseCode = ResponseCodeConst.Error;
                responseMessage.ResponseMessage = "Giao dịch không thành công từ nhà cung cấp";
                //responseMessage.ExtraInfo = string.Join("|", result.Data.ErrorCode, result.Data.ErrorMsg);
                responseMessage.ProviderResponseCode = result?.Data.ErrorCode;
                responseMessage.ProviderResponseMessage = result?.Data.ErrorMsg;
            }
        }
        else
        {
            _logger.LogInformation($"{transCodeToCheck} Error send request");
            responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
            responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
        }

        return responseMessage;
    }

    public async Task<NewMessageReponseBase<InvoiceResultDto>> QueryAsync(PayBillRequestLogDto payBillRequestLog)
    {
        _logger.LogInformation($"{payBillRequestLog.TransCode} ViettelVttConnector Query request: " +
                               payBillRequestLog.ToJson());
        var responseMessage = new NewMessageReponseBase<InvoiceResultDto>
        {
            ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Truy vấn thông tin không thành công")
        };
        var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(payBillRequestLog.ProviderCode);

        if (providerInfo == null)
            return responseMessage;


        if (!_topupGatewayService.ValidConnector(ProviderConst.VTT, providerInfo.ProviderCode))
        {
            _logger.LogError(
                $"{payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{providerInfo.ProviderCode}-ViettelVttConnector ProviderConnector not valid");
            responseMessage.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error,
                "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ");
            return responseMessage;
        }


        var encryptedPassword = Encrypt(providerInfo.Password, providerInfo.PublicKey); //"changemeplease123a@");

        var data = new DataObject
        {
            OrderId = payBillRequestLog.TransCode,
            ServiceCode = "100000",
            BillingCode = payBillRequestLog.ReceiverInfo,
            Username = providerInfo.Username, // "partnerchain",
            Password = encryptedPassword //Encrypt("changemeplease123a@")
        }.ToJson();

        var command = new ViettelPayRequest
        {
            Command = "GET_TELECHARGE_VT_INFO",
            Data = data,
            Sign = Sign(data, providerInfo.PrivateKeyFile)
        };

        _logger.LogInformation("ViettelConnector send: " + command.ToJson());
        var result = await CallApi(providerInfo.ApiUrl, providerInfo.Timeout, command);

        if (result != null)
        {
            _logger.LogInformation(
                $"{payBillRequestLog.ProviderCode}-{payBillRequestLog.TransCode} ViettelVttConnector return: {payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{result.ToJson()}");
            if (result.Data.ErrorCode == "00")
            {
                var dto = new InvoiceResultDto
                {
                    Amount = result.Data.Amount
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
                var reResult = await _topupGatewayService.GetResponseMassageCacheAsync("VIETTEL", result.Data.ErrorCode,
                    payBillRequestLog.TransCode);
                responseMessage.ResponseStatus.ErrorCode = ResponseCodeConst.ResponseCode_WaitForResult;
                responseMessage.ResponseStatus.Message =
                    reResult != null
                        ? reResult.ReponseName
                        : "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
            }
            else
            {
                var reResult = await _topupGatewayService.GetResponseMassageCacheAsync("VIETTEL", result.Data.ErrorCode,
                    payBillRequestLog.TransCode);
                responseMessage.ResponseStatus.ErrorCode =
                    reResult != null ? reResult.ReponseCode : ResponseCodeConst.Error;
                responseMessage.ResponseStatus.Message = reResult != null ? reResult.ReponseName : result.Data.ErrorMsg;
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
        _logger.LogInformation($"{cardRequestLog.TransCode} ViettelVttConnector card request: " +
                               cardRequestLog.ToJson());
        var responseMessage = new MessageResponseBase();
        var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(cardRequestLog.ProviderCode);

        if (providerInfo == null)
            return responseMessage;

        if (!_topupGatewayService.ValidConnector(ProviderConst.VTT, providerInfo.ProviderCode))
        {
            _logger.LogError(
                $"{cardRequestLog.TransCode}-{cardRequestLog.TransRef}-{providerInfo.ProviderCode}-ViettelVttConnector ProviderConnector not valid");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
            };
        }


        var encryptedPassword = Encrypt(providerInfo.Password, providerInfo.PublicKey); //"changemeplease123a@");

        var data = new DataObject
        {
            Amount = (int)cardRequestLog.TransAmount,
            OrderId = cardRequestLog.TransCode,
            ChannelInfo = new ChannelInfo
            {
                ChannelType = "website",
                WebsiteName = "MobileGo",
                WebsiteAddress = "mobilego.vn",
                Source = "internetBanking",
                AccNo = providerInfo.ExtraInfo.Split('|')[1], //"1201100053003",
                BankCode = providerInfo.ExtraInfo.Split('|')[0] //"MB"
            },
            PayerMsisdn = providerInfo.ApiUser, // "84983103127",
            ServiceCode = "PCEBATCH",
            Quantity = cardRequestLog.Quantity,
            Username = providerInfo.Username, //"partnerchain",
            Password = encryptedPassword //Encrypt("changemeplease123a@")
        }.ToJson();
        responseMessage.TransCodeProvider = cardRequestLog.TransCode;

        var command = new ViettelPayRequest
        {
            Command = "PAY_PINCODE_VT_BATCH",
            Data = data,
            Sign = Sign(data, providerInfo.PrivateKeyFile)
        };

        _logger.LogInformation($"{cardRequestLog.TransCode} ViettelConnector send: " + command.ToJson());
        var result = await CallApi(providerInfo.ApiUrl, providerInfo.Timeout, command);

        if (result != null)
        {
            _logger.LogInformation(
                $"Card return: {cardRequestLog.TransCode}-{cardRequestLog.TransRef}-{result.ToJson()}");
            cardRequestLog.ModifiedDate = DateTime.Now;
            cardRequestLog.ResponseInfo = result.Data.ToJson();
            if (result.Data.ErrorCode == "00")
            {
                cardRequestLog.Status = TransRequestStatus.Success;
                responseMessage.ResponseCode = ResponseCodeConst.Success;
                responseMessage.ProviderResponseTransCode = result.Data.TransId;
                responseMessage.ResponseMessage = "Giao dịch thành công";
                try
                {
                    var cards = Decrypt(result.Data.BillDetail, providerInfo.PrivateKeyFile);
                    if (!string.IsNullOrEmpty(cards))
                    {
                        var viettelCards = cards.FromJson<List<ViettelCards>>();
                        var cardList = new List<CardRequestResponseDto>();
                        foreach (var viettelCard in viettelCards)
                            cardList.Add(new CardRequestResponseDto
                            {
                                CardType = "VTE",
                                CardValue = viettelCard.Amount,
                                CardCode = viettelCard.Pincode,
                                Serial = viettelCard.Serial,
                                ExpireDate = DateTime.ParseExact(viettelCard.Expire, "yyyyMMddHHmmss",
                                    CultureInfo.InvariantCulture).ToString("d")
                            });
                        responseMessage.Payload = cardList;
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError($"{cardRequestLog.TransCode} Error decrypt cards: " + e.Message);
                }
            }
            else if (new[] { "32", "232", "233", "605", "K02", "650", "501102" }.Contains(result.Data.ErrorCode))
            {
                var reResult = await _topupGatewayService.GetResponseMassageCacheAsync("VIETTEL", result.Data.ErrorCode,
                    cardRequestLog.TransCode);
                cardRequestLog.Status = TransRequestStatus.Timeout;
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                responseMessage.ResponseMessage =
                    reResult != null
                        ? reResult.ReponseName
                        : "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
            }
            else
            {
                var reResult = await _topupGatewayService.GetResponseMassageCacheAsync("VIETTEL", result.Data.ErrorCode,
                    cardRequestLog.TransCode);
                cardRequestLog.Status = TransRequestStatus.Fail;
                responseMessage.ResponseCode = reResult != null ? reResult.ReponseCode : ResponseCodeConst.Error;
                responseMessage.ResponseMessage = reResult != null ? reResult.ReponseName : result.Data.ErrorMsg;
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

        if (!_topupGatewayService.ValidConnector(ProviderConst.VTT, providerInfo.ProviderCode))
        {
            _logger.LogError($"{providerCode}-{transCode}-ViettelVttConnector ProviderConnector not valid");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
            };
        }


        var encryptedPassword = Encrypt(providerInfo.Password, providerInfo.PublicKey); //"changemeplease123a@");

        var data = new DataObject
        {
            OrderId = transCode,
            ServiceCode = "CP",
            Username = providerInfo.Username, //"partnerchain",
            Password = encryptedPassword //Encrypt("changemeplease123a@")
        }.ToJson();

        var command = new ViettelPayRequest
        {
            Command = "CHECK_CP",
            Data = data,
            Sign = Sign(data, providerInfo.PrivateKeyFile)
        };

        _logger.LogInformation($"{transCode} Balance object send: " + command.ToJson());
        var result = await CallApi(providerInfo.ApiUrl, providerInfo.Timeout, command);

        if (result != null)
        {
            _logger.LogInformation($"{transCode} Balance return: {transCode}-{result.ToJson()}");
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
                var reResult =await _topupGatewayService.GetResponseMassageCacheAsync("VIETTEL", result.Data.ErrorCode,transCode);
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                responseMessage.ResponseMessage = reResult != null
                        ? reResult.ReponseName
                        : "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
            }
            else
            {
                var reResult =await _topupGatewayService.GetResponseMassageCacheAsync("VIETTEL", result.Data.ErrorCode,transCode);
                responseMessage.ResponseCode = reResult != null ? reResult.ReponseCode : ResponseCodeConst.Error;
                responseMessage.ResponseMessage = reResult != null ? reResult.ReponseName : result.Data.ErrorMsg;
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
        _logger.LogInformation("Get deposit request: " + request.TransCode + "|" + request.Amount);
        var responseMessage = new MessageResponseBase();
        var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync("VIETTEL");

        if (providerInfo == null)
            return responseMessage;

        if (!_topupGatewayService.ValidConnector(ProviderConst.VTT, providerInfo.ProviderCode))
        {
            _logger.LogError(
                $"{request.TransCode}-{request.Amount}-{providerInfo.ProviderCode}-ViettelVttConnector ProviderConnector not valid");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
            };
        }


        var encryptedPassword = Encrypt(providerInfo.Password, providerInfo.PublicKey); //"changemeplease123a@");

        var data = new DataObject
        {
            OrderId = request.TransCode,
            ServiceCode = "PAYMENT",
            Username = providerInfo.Username, //"partnerchain",
            Password = encryptedPassword, //Encrypt("changemeplease123a@")
            Amount = (int)request.Amount,
            AccNo = providerInfo.ExtraInfo.Split('|')[1], //"1201100053003",
            BankCode = providerInfo.ExtraInfo.Split('|')[0] //"MB"
        }.ToJson();

        var command = new ViettelPayRequest
        {
            Command = "PAY_IN_PREPAID",
            Data = data,
            Sign = Sign(data, providerInfo.PrivateKeyFile)
        };

        _logger.LogInformation("Deposit object send: " + command.ToJson());
        var result = await CallApi(providerInfo.ApiUrl, providerInfo.Timeout, command);

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
                    await _topupGatewayService.GetResponseMassageCacheAsync("VIETTEL", result.Data.ErrorCode,
                        request.TransCode);
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                responseMessage.ResponseMessage =
                    reResult != null
                        ? reResult.ReponseName
                        : "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
            }
            else
            {
                var reResult =
                    await _topupGatewayService.GetResponseMassageCacheAsync("VIETTEL", result.Data.ErrorCode,
                        request.TransCode);
                responseMessage.ResponseCode = ResponseCodeConst.Error;
                responseMessage.ResponseMessage = reResult != null ? reResult.ReponseName : result.Data.ErrorMsg;
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

    #region Private

    private async Task<ViettelPayResponse> CallApi(string url, int timeout, ViettelPayRequest viettelPayRequest)
    {
        var envelop = CreateSoapEnvelope(viettelPayRequest);
        var exeption = string.Empty;
        var responseString = string.Empty;
        var retryCount = 0;

        do
        {
            try
            {
                //gunner xem lại chỗ này sau khi update version
                responseString = await url.PostXmlToUrlAsync(envelop.InnerXml, httpReq =>
                {
                    httpReq.Headers.Clear();
                    httpReq.Headers.Add("SOAPAction", "process");
                    //httpReq.ContentType = "text/xml;charset=\"utf-8\"";
                    //httpReq.Accept = "text/xml";
                    //httpReq.Timeout = timeout * 1000;
                });
                retryCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError("Trans exception: " + ex.Message);
                exeption = ex.Message;
                responseString = "TIMEOUT";
            }
        } while (string.IsNullOrEmpty(responseString) && retryCount < 3);

        if (!string.IsNullOrEmpty(responseString))
        {
            if (responseString == "TIMEOUT")
                return new ViettelPayResponse
                {
                    Data = new DataObject
                    {
                        ErrorCode = "501102", //Định nghĩa mã lỗi cho trường hợp gọi Service timeout
                        ErrorMsg = exeption
                    }
                };

            responseString = responseString.Replace("&quot;", "\"");
            var soapEnvelopeDocument = new XmlDocument();
            soapEnvelopeDocument.LoadXml(responseString);

            var responseMessage = soapEnvelopeDocument.InnerText.FromJson<ViettelPayResponse>();
            return responseMessage;
        }

        return null;
    }

    private static XmlDocument CreateSoapEnvelope(ViettelPayRequest request)
    {
        var soapEnvelopeDocument = new XmlDocument();
        soapEnvelopeDocument.LoadXml(string.Format(VIETTEL_SOAP, request.Command, request.Data, request.Sign));
        return soapEnvelopeDocument;
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
        var privateKeyBytes = Convert.FromBase64String(privateKeyBlocks[1]);

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