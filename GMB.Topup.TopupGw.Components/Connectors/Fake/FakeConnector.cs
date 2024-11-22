using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GMB.Topup.Shared;
using GMB.Topup.Shared.Dtos;
using GMB.Topup.Shared.Utils;
using GMB.Topup.TopupGw.Contacts.ApiRequests;
using GMB.Topup.TopupGw.Contacts.Dtos;
using GMB.Topup.TopupGw.Contacts.Enums;
using GMB.Topup.TopupGw.Domains.BusinessServices;
using GMB.Topup.TopupGw.Domains.Entities;
using MassTransit;
using Microsoft.Extensions.Logging;
using MongoDB.Driver.Core.WireProtocol.Messages;
using ServiceStack;

namespace GMB.Topup.TopupGw.Components.Connectors.Fake;

public class FakeConnector : IGatewayConnector
{
    private readonly ILogger<FakeConnector> _logger;
    private readonly ITopupGatewayService _topupGatewayService;

    public FakeConnector(ITopupGatewayService topupGatewayService, ILogger<FakeConnector> logger)
    {
        _topupGatewayService = topupGatewayService;
        _logger = logger;
    }

    public async Task<MessageResponseBase> TopupAsync(TopupRequestLogDto topupRequestLog, ProviderInfoDto providerInfo)
    {
        Console.WriteLine(topupRequestLog.ToJson());
        _logger.LogInformation("Get topup request: " + topupRequestLog.ToJson());
        if (!_topupGatewayService.ValidConnector(ProviderConst.FAKE, topupRequestLog.ProviderCode))
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
            };

        var responseMessage = new MessageResponseBase { TransCodeProvider = topupRequestLog.TransCode };

        // var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(topupRequestLog.ProviderCode);
        //
        // if (providerInfo == null)
        // {
        //     Console.WriteLine("providerInfo is null");
        //     return responseMessage;
        // }

        switch (topupRequestLog.ReceiverInfo)
        {
            case "0988000001":
                topupRequestLog.Status = TransRequestStatus.Fail;
                responseMessage.ResponseCode = ResponseCodeConst.Error;
                responseMessage.ResponseMessage = "GD thất bại";
                break;
            case "0988000002":
                topupRequestLog.Status = TransRequestStatus.Fail;
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_PhoneLocked;
                responseMessage.ResponseMessage = "Thuê bao đang bị khóa chiều nạp";
                responseMessage.ProviderResponseCode = "K85";
                responseMessage.ProviderResponseMessage =
                    "Thuê bao chưa được kích hoạt hoặc không phải nhà mạng yêu cầu";
                break;
            case "0988000023":
                await Task.Delay(TimeSpan.FromMinutes(1));
                topupRequestLog.Status = TransRequestStatus.Fail;
                responseMessage.ResponseCode = "4005";
                responseMessage.ResponseMessage = "Giao dịch TimeOut";
                break;
            case "0969898836":
                //await Task.Delay(TimeSpan.FromMinutes(1));
                topupRequestLog.Status = TransRequestStatus.Success; //ok
                responseMessage.ResponseCode = ResponseCodeConst.Success;
                responseMessage.ResponseMessage = "Giao dịch thành công";
                responseMessage.ProviderResponseTransCode = DateTime.Now.ToString("yyyyMMddHHmmssfff");
                responseMessage.ReceiverType = "TS";
                break;
            case "0969898837":
                await Task.Delay(TimeSpan.FromMinutes(1));
                topupRequestLog.Status = TransRequestStatus.Fail;
                responseMessage.ResponseCode = ResponseCodeConst.Error;
                responseMessage.ResponseMessage = "Giao dịch lỗi";
                break;
            case "0969898838":
                await Task.Delay(TimeSpan.FromMinutes(1));
                topupRequestLog.Status = TransRequestStatus.Timeout;
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                responseMessage.ResponseMessage = "Chưa có KQ";
                break;
            case "0988000004":
                await Task.Delay(TimeSpan.FromSeconds(15));
                topupRequestLog.Status = TransRequestStatus.Fail;
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_InProcessing;
                responseMessage.ResponseMessage = "Giao dịch đang xử lý";
                break;
            case "0988000005":
                await Task.Delay(TimeSpan.FromSeconds(10));
                topupRequestLog.Status = TransRequestStatus.Fail;
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                responseMessage.ResponseMessage =
                    "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                break;
            case "0988000006":
                topupRequestLog.Status = TransRequestStatus.Fail; //TEST
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_Paid;
                responseMessage.ResponseMessage = "Giao dịch đã trừ tiền khách hàng";
                break;
            case "0988000007":
                topupRequestLog.Status = TransRequestStatus.Fail;
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_PhoneLocked;
                responseMessage.ResponseMessage = "Số điện thoại khách hàng đã bị khóa";
                break;
            case "0988000008":
                topupRequestLog.Status = TransRequestStatus.Fail;
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_PhoneNotValid;
                responseMessage.ResponseMessage = "Số điện thoại không hợp lệ";
                break;
            case "0988000009":
                topupRequestLog.Status = TransRequestStatus.Fail;
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_NotValidStatus;
                responseMessage.ResponseMessage =
                    "Giao dịch không thành công. Vui lòng kiểm tra thông tin của thuê bao";
                break;
            case "0988000010":
                topupRequestLog.Status = TransRequestStatus.Fail;
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_PhoneLockTopup;
                responseMessage.ResponseMessage = "Số điện thoại bị khóa";
                break;

            //khangpv bổ sung fake mã lỗi - 2022-07-05
            case "0988000011":
                topupRequestLog.Status = TransRequestStatus.Fail;
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_NotEzpay;
                responseMessage.ResponseMessage = "Thuê bao chưa đăng ký EZPay.";
                break;

            case "0988000012":
                topupRequestLog.Status = TransRequestStatus.Fail;
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_ErrorProvider;
                responseMessage.ResponseMessage = "Giao dịch lỗi từ phía NCC";
                break;
            case "0988000013":
                topupRequestLog.Status = TransRequestStatus.Fail;
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_ServiceConfigNotValid;
                responseMessage.ResponseMessage = "Dịch vụ chưa được cấu hình";
                break;
            case "0988000014":
                topupRequestLog.Status = TransRequestStatus.Fail;
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_ProductNotFound;
                responseMessage.ResponseMessage = "Sản phẩm không tồn tại, không được hỗ trợ";
                break;


            default:
                topupRequestLog.Status = TransRequestStatus.Success;
                responseMessage.ResponseCode = ResponseCodeConst.Success;
                responseMessage.ResponseMessage = "GD thành công";
                responseMessage.ProviderResponseTransCode = DateTime.Now.ToString("yyyyMMddHHmmssfff");
                responseMessage.ReceiverType = "TT";
                break;
        }

        await _topupGatewayService.TopupRequestLogUpdateAsync(topupRequestLog);

        return responseMessage;
    }

    public async Task<MessageResponseBase> TransactionCheckAsync(string providerCode, string transCodeToCheck,
        string transCode, string serviceCode = null, ProviderInfoDto providerInfo = null)
    {
        if (serviceCode != null && serviceCode.StartsWith("PIN"))
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
            var responseMessage = new MessageResponseBase();
            responseMessage.ResponseCode = ResponseCodeConst.Success;
            responseMessage.ResponseMessage = "Giao dịch thành công";
            responseMessage.ProviderResponseCode = ResponseCodeConst.Success;
            responseMessage.ProviderResponseMessage = "Thành công";
            responseMessage.ProviderResponseTransCode = "";
            var rdm = new Random();
            var rdm1 = new Random();
            var date = DateTime.Now;
            var cardList = new List<CardRequestResponseDto>();
            var cardCode = DateTime.Now.ToString("yyMMddHHmmss");
            for (var i = 0; i < 10; i++)
                cardList.Add(new CardRequestResponseDto
                {
                    CardCode = ("C" + cardCode + rdm.Next(1000, 9999)).EncryptTripDes(),
                    Serial = "S" + cardCode + +rdm1.Next(1000, 9999),
                    ExpireDate = date.AddYears(3).ToString("dd/MM/yyyy"),
                    ExpiredDate = date.AddYears(3),
                    CardType = "VTE",
                    CardValue = "10000"
                });

            responseMessage.Payload = cardList;

            return responseMessage;
        }
        return await Task.FromResult(new MessageResponseBase());
    }

    public Task<NewMessageResponseBase<InvoiceResultDto>> QueryAsync(PayBillRequestLogDto payBillRequestLog)
    {
        //const string fake = "{\"status\":{\"value\":\"Success!\"},\"invoice\":{\"invoiceId\":\"70945037\",\"serviceId\":\"54894010\",\"invoiceReference\":\"\",\"customerReference\":\"PB17050051761\",\"amount\":148500,\"currency\":\"VND\",\"info\":\"989671017;14101045;6/2021;210610TD|add_info=210619609476592\",\"creationDate\":\"2021-06-19T05:01:38Z\",\"isPartialPaymentAllowed\":\"false\",\"invoiceAttributes\":[{\"invoiceId\":\"70945037\",\"invoiceAttributeTypeId\":\"CUS_ADDRESS\",\"value\":\"Lê Văn A, Thị Trấn Tân Phong, Huyện Đông Tà, Tỉnh Hà Nam\",\"created\":\"2021-06-19T05:01:38Z\"},{\"invoiceId\":\"70945037\",\"invoiceAttributeTypeId\":\"INV_PERIOD\",\"value\":\"06/2021\",\"created\":\"2021-06-19T05:01:38Z\"},{\"invoiceId\":\"70945037\",\"invoiceAttributeTypeId\":\"CUS_NAME\",\"value\":\"Tran Thi Van A\",\"created\":\"2021-06-19T05:01:38Z\"}],\"setCustomerReference\":true,\"setCreationDate\":true,\"setInvoiceReference\":true,\"setInvoiceAttributes\":true,\"setCurrency\":true,\"setStatus\":true,\"setAmount\":true}}";
        //var response = fake.FromJson<ZoTaResponse>();
        // return await Task.FromResult(new NewMessageReponseBase<InvoiceResultDto>
        // {
        //     ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Success, "Giao dịch thành công"),
        //     Results = new InvoiceResultDto
        //     {
        //         Address = "Số 36/21 ngõ 452 Hồ Tùng Mậu - Hà Nội",
        //         CustomerName = "Khách hàng ẩn danh",
        //         Amount = 165000,
        //         Period = DateTime.Now.ToString("dd-MM"),
        //         CustomerReference = payBillRequestLog.ReceiverInfo
        //     }
        // });
        return Task.FromResult(payBillRequestLog.ReceiverInfo switch
        {
            "091231232" => new NewMessageResponseBase<InvoiceResultDto>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_ErrorProvider,
                    "Giao dịch lỗi từ NCC"),
            },
            "091231231" => new NewMessageResponseBase<InvoiceResultDto>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_InvoiceHasBeenPaid,
                    "Hóa đơn đã được thanh toán hoặc chưa phát sinh nợ cước"),
            },
            "091231230" => new NewMessageResponseBase<InvoiceResultDto>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Success, "Giao dịch thành công"),
                Results = new InvoiceResultDto
                {
                    Amount = 210000,
                    CustomerReference = payBillRequestLog.ReceiverInfo
                }
            },
            _ => new NewMessageResponseBase<InvoiceResultDto>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Success, "Giao dịch thành công"),
                Results = new InvoiceResultDto
                {
                    Address = "Số 36/21 ngõ 452 Hồ Tùng Mậu - Hà Nội",
                    CustomerName = "Khách hàng ẩn danh",
                    Amount = 165000,
                    Period = DateTime.Now.ToString("dd-MM"),
                    CustomerReference = payBillRequestLog.ReceiverInfo
                }
            }
        });
    }

    public async Task<MessageResponseBase> CardGetByBatchAsync(CardRequestLogDto cardRequestLog)
    {
        _logger.LogInformation("Get card request: " + cardRequestLog.ToJson());
        var responseMessage = new MessageResponseBase();

        // var providerInfo = await _topupGatewayService.ProviderInfoGetAsync(cardRequestLog.ProviderCode);
        //
        // if (providerInfo == null)
        //     return responseMessage;
        if (cardRequestLog.Quantity >= 1000000)
        {
            responseMessage.TransCodeProvider = cardRequestLog.TransCode;
            responseMessage.ResponseCode = "4009";
            responseMessage.ResponseMessage = "Kho thẻ không đủ";
            cardRequestLog.Status = TransRequestStatus.Success;
        }
        else
        {
            await Task.Delay(TimeSpan.FromSeconds(15));
            var rdm = new Random();
            var rdm1 = new Random();
            responseMessage.TransCodeProvider = cardRequestLog.TransCode;
            responseMessage.ResponseCode = cardRequestLog.Quantity < 100 ? ResponseCodeConst.Success : ResponseCodeConst.ResponseCode_TimeOut;
            responseMessage.ResponseMessage = "Giao dịch thành công";
            cardRequestLog.Status = TransRequestStatus.Success;

            var date = DateTime.Now;
            var cardCode = DateTime.Now.ToString("yyMMddHHmmss");
            var cardList = new List<CardRequestResponseDto>();
            if (cardRequestLog.ProductCode.StartsWith("VNA"))
            {
                for (var i = 0; i < cardRequestLog.Quantity; i++)
                    cardList.Add(new CardRequestResponseDto
                    {
                        CardType = cardRequestLog.Vendor,
                        CardValue = cardRequestLog.TransAmount.ToString("0"),
                        CardCode = "C" + cardCode + rdm.Next(1000, 9999),
                        Serial = "S" + cardCode + rdm1.Next(1000, 9999),
                        ExpireDate = date.AddYears(3).ToString("dd/MM/yyyy"),
                        ExpiredDate = date.AddYears(3),
                    });
            }
            else if (cardRequestLog.ProductCode.StartsWith("VTE"))
            {
                for (var i = 0; i < cardRequestLog.Quantity; i++)
                    cardList.Add(new CardRequestResponseDto
                    {
                        CardType = cardRequestLog.Vendor,
                        CardValue = cardRequestLog.TransAmount.ToString("0"),
                        CardCode = "C" + cardCode + rdm.Next(1000, 9999),
                        Serial = "S" + cardCode + rdm1.Next(1000, 9999),
                        ExpireDate = date.AddYears(3).ToString("dd/MM/yyyy"),
                        ExpiredDate = date.AddYears(3),
                    });
            }
            else
            {
                for (var i = 0; i < cardRequestLog.Quantity; i++)
                    cardList.Add(new CardRequestResponseDto
                    {
                        CardType = cardRequestLog.Vendor,
                        CardValue = cardRequestLog.TransAmount.ToString("0"),
                        CardCode = "C" + cardCode + rdm.Next(1000, 9999),
                        Serial = "S" + cardCode + rdm1.Next(1000, 9999),
                        ExpireDate = date.AddYears(3).ToString("dd/MM/yyyy"),
                        ExpiredDate = date.AddYears(3)
                    });
            }

            responseMessage.Payload = cardList;
        }


        await _topupGatewayService.CardRequestLogUpdateAsync(cardRequestLog);
        return responseMessage;
    }

    public async Task<MessageResponseBase> CheckBalanceAsync(string providerCode, string transCode)
    {
        return await Task.FromResult(new MessageResponseBase());
    }

    public async Task<MessageResponseBase> DepositAsync(DepositRequestDto request)
    {
        return await Task.FromResult(new MessageResponseBase());
    }

    public async Task<MessageResponseBase> PayBillAsync(PayBillRequestLogDto payBillRequestLog)
    {
        _logger.LogInformation("Get Paybill request: " + payBillRequestLog.ToJson());
        var responseMessage = new MessageResponseBase();

        // var providerInfo = await _topupGatewayService.ProviderInfoGetAsync(payBillRequestLog.ProviderCode);
        //
        // if (providerInfo == null)
        //     return responseMessage;

        // var client = new JsonServiceClient(providerInfo.ApiUrl)
        //     {Timeout = TimeSpan.FromMilliseconds(providerInfo.Timeout)}; //("http://dev.api.zo-ta.com");
        //
        // var providerService =
        //     providerInfo.ProviderServices.Find(p => p.ProductCode == payBillRequestLog.ProductCode);
        // var serviceCode = string.Empty;
        // if (providerService != null)
        //     serviceCode = providerService.ServiceCode;
        // else
        //     _logger.Warn($"ProviderService with ProductCode [{payBillRequestLog.ProductCode}] is null");


        payBillRequestLog.Status = TransRequestStatus.Success;
        responseMessage.ResponseCode = ResponseCodeConst.Success;
        responseMessage.ResponseMessage = "Giao dịch thành công";

        await _topupGatewayService.PayBillRequestLogUpdateAsync(payBillRequestLog);


        return responseMessage;
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