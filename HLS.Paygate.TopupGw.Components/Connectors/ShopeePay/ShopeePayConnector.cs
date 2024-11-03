using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.CacheManager;
using HLS.Paygate.Shared.Dtos;
using HLS.Paygate.Shared.Utils;
using HLS.Paygate.TopupGw.Contacts.Dtos;
using HLS.Paygate.TopupGw.Contacts.Enums;
using HLS.Paygate.TopupGw.Domains.BusinessServices;
using HLS.Paygate.TopupGw.Domains.Entities;
using MassTransit;
using Microsoft.Extensions.Logging;
using MongoDB.Driver.Linq;
using Newtonsoft.Json;
using Paygate.Contracts.Commands.Commons;
using Paygate.Contracts.Requests.Commons;
using ServiceStack;

namespace HLS.Paygate.TopupGw.Components.Connectors.ShopeePay;

public class ShopeePayConnector : GatewayConnectorBase
{
    private readonly ICacheManager _cacheManager;
    private readonly ILogger<ShopeePayConnector> _logger;
    private readonly IBus _bus;

    public ShopeePayConnector(ITopupGatewayService topupGatewayService, IBus bus,
        ILogger<ShopeePayConnector> logger, ICacheManager cacheManager) : base(topupGatewayService)
    {
        _logger = logger;
        _cacheManager = cacheManager;
        _bus = bus;
    }

    public override async Task<MessageResponseBase> TopupAsync(TopupRequestLogDto topupRequestLog,
        ProviderInfoDto providerInfo)
    {
        throw new NotImplementedException();
    }

    public override async Task<MessageResponseBase> TransactionCheckAsync(string providerCode, string transCodeToCheck,
        string transCode, string serviceCode = null, ProviderInfoDto providerInfo = null)
    {
        try
        {
            _logger.LogInformation($"{transCodeToCheck}-ShopeePayConnector Check request: " + transCode);
            var responseMessage = new MessageResponseBase();

            if (providerInfo == null)
                providerInfo = await TopupGatewayService.ProviderInfoCacheGetAsync(providerCode);

            if (providerInfo == null ||
                !TopupGatewayService.ValidConnector(ProviderConst.SHOPEEPAY, providerInfo.ProviderCode))
            {
                _logger.LogError($"{transCode}-{providerCode}-ShopeePayConnector ProviderConnector not valid");
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult,
                    ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ"
                };
            }

            var data = new ShopeeGetCardRequest
            {
                Account = providerInfo.Username,
                ReferenceNo = transCodeToCheck,
                PartnerId = providerInfo.ApiUser
            };

            var plainText = $"account={data.Account}&partner_id={data.PartnerId}&reference_no={data.ReferenceNo}";
            data.Signature = Sign(plainText, providerInfo.PrivateKeyFile);
            _logger.LogInformation("{TransCode} ShopeePayConnector send: {Data}", transCodeToCheck, data.ToJson());
            var result = await CallApiShopee(providerInfo, "chain_store/get_card_v2", data.ToJson(), transCodeToCheck);
            _logger.LogInformation("ShopeePayConnector GetCard_CallApi return: {TransCode}-{TransRef}-{Return}",
                transCodeToCheck, transCode, result.ToJson());

            if (result != null)
            {
                int status = Convert.ToInt32(result.resp_code);
                responseMessage.ProviderResponseCode = result.resp_code;
                responseMessage.ProviderResponseMessage = result.resp_code;
                if (status == 0)
                {
                    try
                    {
                        var valueInfo = ToDictionary<object>(result.order_info).First().Value.ToString();
                        var orderCard = valueInfo.FromJson<orderInfo>();
                        if (orderCard.status == 0)
                        {
                            responseMessage.ResponseCode = ResponseCodeConst.Success;
                            responseMessage.ResponseMessage = "Giao dịch thành công";
                            var cardList = new List<CardRequestResponseDto>();
                            foreach (var card in orderCard.cards)
                                cardList.Add(new CardRequestResponseDto
                                {
                                    CardCode = card.pin,
                                    Serial = card.serial,
                                    ExpireDate = card.expiry,
                                    ExpiredDate = DateTime.ParseExact(card.expiry, "yyyy-MM-dd",
                                        CultureInfo.InvariantCulture),
                                });
                            cardList = GenDecryptListCode(providerInfo.PrivateKeyFile, cardList,
                                isEncryptTripDes: true);
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
                        else if (orderCard.status == 1 || orderCard.status == 3)
                        {
                            responseMessage.ResponseCode = ResponseCodeConst.Error;
                            responseMessage.ResponseMessage = "Giao dịch không thành công";
                        }
                        else
                        {
                            responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                            responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả xử lý.";
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogError($"TransCode= {transCodeToCheck} ShopeePayConnector Error parsing cards: " +
                                         e.Message);
                    }
                }
                else
                {
                    // var arrayErrors = TopupGatewayService.ConvertArrayCode(providerInfo.ExtraInfo ?? string.Empty);
                    // if (arrayErrors.Contains(status))
                    // {
                    //     responseMessage.ResponseCode = ResponseCodeConst.Error;
                    //     responseMessage.ResponseMessage = "Giao dịch không thành công";
                    // }
                    // else
                    // {
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả xử lý.";
                    //}
                }
            }
            else
            {
                _logger.LogInformation($"{transCodeToCheck} Error send request");
                responseMessage.ResponseMessage = "Lỗi kết nối nhà cung cấp";
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
        _logger.LogInformation($"{cardRequestLog.TransCode} ShopeePayConnector card request: " +
                               cardRequestLog.ToJson());
        var responseMessage = new MessageResponseBase();
        var providerInfo = await TopupGatewayService.ProviderInfoCacheGetAsync(cardRequestLog.ProviderCode);

        if (providerInfo == null)
            return responseMessage;

        if (!TopupGatewayService.ValidConnector(ProviderConst.SHOPEEPAY, providerInfo.ProviderCode))
        {
            _logger.LogError(
                $"{cardRequestLog.TransCode}-{cardRequestLog.TransRef}-{providerInfo.ProviderCode}-ShopeePayConnector ProviderConnector not valid");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
            };
        }

        var product = providerInfo.ProviderServices.Find(p => p.ProductCode == cardRequestLog.ProductCode);
        if (product == null)
        {
            _logger.LogError(
                $"{cardRequestLog.TransCode}-{cardRequestLog.TransRef}-{cardRequestLog.ProviderCode}-ESaleConnector ProviderConnector not config productCode= {cardRequestLog.ProductCode}");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Giao dịch lỗi. Chưa cấu hình sản phẩm"
            };
        }

        string serviceCode = product.ServiceCode;
        var data = new ShopeeRequest
        {
            Account = providerInfo.Username,
            ReferenceNo = cardRequestLog.TransCode,
            Quantity = cardRequestLog.Quantity.ToString(),
            PartnerId = providerInfo.ApiUser,
            ServiceCode = serviceCode,
        };

        var plainText =
            $"account={data.Account}&partner_id={data.PartnerId}&quantity={data.Quantity}&reference_no={data.ReferenceNo}&service_code={data.ServiceCode}";
        data.Signature = Sign(plainText, providerInfo.PrivateKeyFile);

        responseMessage.TransCodeProvider = cardRequestLog.TransCode;
        _logger.LogInformation("{TransCode} ShopeePayConnector send: {Data}", cardRequestLog.TransCode, data.ToJson());
        var result = await CallApiShopee(providerInfo, "chain_store/purchase_card_v2", data.ToJson(),
            cardRequestLog.TransCode);
        _logger.LogInformation("ShopeePayConnector CallApi return: {TransCode}-{TransRef}-{Return}",
            cardRequestLog.TransCode,
            cardRequestLog.TransRef, result.ToJson());

        if (result != null)
        {
            cardRequestLog.ResponseInfo = "";
            cardRequestLog.ModifiedDate = DateTime.Now;
            _logger.LogInformation("{ProviderCode}{TransCode} ShopeePayConnector return: {TransRef}-{Json}",
                cardRequestLog.ProviderCode, cardRequestLog.TransCode, cardRequestLog.TransRef, result.ToJson());

            int status = Convert.ToInt32(result.resp_code);
            responseMessage.ProviderResponseCode = result.resp_code;
            responseMessage.ProviderResponseMessage = result.resp_msg;
            if (status == 0)
            {
                cardRequestLog.Status = TransRequestStatus.Success;
                responseMessage.ResponseCode = ResponseCodeConst.Success;
                try
                {
                    var cardList = new List<CardRequestResponseDto>();
                    foreach (var card in result.cards)
                        cardList.Add(new CardRequestResponseDto
                        {
                            CardType = cardRequestLog.Vendor,
                            CardValue = cardRequestLog.TransAmount.ToString(),
                            CardCode = card.pin,
                            Serial = card.serial,
                            ExpireDate = card.expiry,
                            ExpiredDate = DateTime.ParseExact(card.expiry, "yyyy-MM-dd",
                                CultureInfo.InvariantCulture),
                        });
                    cardList = GenDecryptListCode(providerInfo.PrivateKeyFile, cardList);
                    responseMessage.Payload = cardList;
                    if (cardList.Count == 0)
                        await SendNoti(cardRequestLog, providerInfo, "Lấy mới thẻ cào");
                }
                catch (Exception e)
                {
                    _logger.LogError($"TransCode= {cardRequestLog.TransCode} PayTechConnector Error parsing cards: " +
                                     e.Message);
                }
            }
            else
            {
                var arrayErrors = TopupGatewayService.ConvertArrayCode(providerInfo.ExtraInfo ?? string.Empty);
                if (arrayErrors.Contains(status))
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
        }
        else
        {
            _logger.LogInformation($"{cardRequestLog.TransCode} Error send request");
            responseMessage.ResponseMessage = "Lỗi kết nối nhà cung cấp";
            cardRequestLog.Status = TransRequestStatus.Fail;
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

        if (!TopupGatewayService.ValidConnector(ProviderConst.SHOPEEPAY, providerInfo.ProviderCode))
        {
            _logger.LogError("{ProviderCode}-{TransCode}-ShopeePayConnector ProviderConnector not valid", providerCode,
                transCode);
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
            };
        }

        var plainText = $"account={providerInfo.Username}&partner_id={providerInfo.ApiUser}";
        var signature = Sign(plainText, providerInfo.PrivateKeyFile);
        var inputDto = new ShopeeBalanceRequest()
        {
            Account = providerInfo.Username,
            PartnerId = providerInfo.ApiUser,
            Signature = signature,
        };

        _logger.LogInformation("{TransCode} Balance object send: {Data}", transCode);
        var result = await CallApiShopee(providerInfo, "chain_store/get_balance", inputDto.ToJson(), transCode);
        if (result != null)
        {
            _logger.LogInformation($"{transCode} Balance return: {transCode}-{result.ToJson()}");
            if (result.resp_code == "0")
            {
                responseMessage.ResponseCode = ResponseCodeConst.Success;
                responseMessage.ResponseMessage = "Giao dịch thành công";
                responseMessage.Payload = result.balance.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                responseMessage.ResponseCode = ResponseCodeConst.Error;
                responseMessage.ResponseMessage = result.resp_msg;
            }
        }
        else
        {
            _logger.LogInformation("{TransCode} Error send request", transCode);
            responseMessage.ResponseMessage = "Lỗi kết nối nhà cung cấp";
        }

        return responseMessage;
    }

    private async Task<ShopeeReponse> CallApiShopee(ProviderInfoDto providerInfo, string function, string request,
        string transCode)
    {
        try
        {
            var client = new JsonServiceClient(providerInfo.ApiUrl)
            {
                Timeout = TimeSpan.FromSeconds(providerInfo.Timeout)
            };
            var res = client.Post<ShopeeReponse>(function, request);
            return res;
        }
        catch (Exception ex)
        {
            _logger.LogError($"TransCode= {transCode}|ShopeePay_CallApiCardCode Exception: {ex}");
            return new ShopeeReponse()
            {
                resp_code = "501102",
                resp_msg = ex.Message,
            };
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
            HashAlgorithmName.SHA1,
            RSASignaturePadding.Pkcs1);
        var signature = Convert.ToBase64String(sig);
        return signature;
    }

    private List<CardRequestResponseDto> GenDecryptListCode(string privateFile,
        List<CardRequestResponseDto> cardList, bool isEncryptTripDes = false)
    {
        try
        {
            var privateKeyText = File.ReadAllText("files/" + privateFile);
            var privateKeyBlocks = privateKeyText.Split("-", StringSplitOptions.RemoveEmptyEntries);
            var privateKeyBytes = Convert.FromBase64String(privateKeyBlocks[1]);
            using var rsa = RSA.Create();
            if (privateKeyBlocks[0] == "BEGIN PRIVATE KEY")
                rsa.ImportPkcs8PrivateKey(privateKeyBytes, out _);
            else if (privateKeyBlocks[0] == "BEGIN RSA PRIVATE KEY")
                rsa.ImportRSAPrivateKey(privateKeyBytes, out _);

            foreach (var item in cardList)
            {
                var souceBytesCode = Convert.FromBase64String(item.CardCode);
                var bytesCode = rsa.Decrypt(souceBytesCode, RSAEncryptionPadding.Pkcs1);
                var stringCode = Encoding.UTF8.GetString(bytesCode, 0, bytesCode.Length);
                item.CardCode = isEncryptTripDes ? stringCode.EncryptTripDes() : stringCode;
            }

            return cardList;
        }
        catch (Exception ex)
        {
            _logger.LogError("GenDecryptListCode exception: " + ex.Message);
            return cardList;
        }
    }

    private static Dictionary<string, TValue> ToDictionary<TValue>(object obj)
    {
        var json = JsonConvert.SerializeObject(obj);
        var dictionary = JsonConvert.DeserializeObject<Dictionary<string, TValue>>(json);
        return dictionary;
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

    [DataContract]
    internal class ShopeeRequest
    {
        [DataMember(Name = "account")] public string Account { get; set; }

        [DataMember(Name = "partner_id")] public string PartnerId { get; set; }

        [DataMember(Name = "quantity")] public string Quantity { get; set; }

        [DataMember(Name = "reference_no")] public string ReferenceNo { get; set; }

        [DataMember(Name = "service_code")] public string ServiceCode { get; set; }

        [DataMember(Name = "signature")] public string Signature { get; set; }
    }

    internal class ShopeeBalanceRequest
    {
        [DataMember(Name = "account")] public string Account { get; set; }

        [DataMember(Name = "partner_id")] public string PartnerId { get; set; }
        [DataMember(Name = "signature")] public string Signature { get; set; }
    }

    internal class ShopeeGetCardRequest
    {
        [DataMember(Name = "account")] public string Account { get; set; }

        [DataMember(Name = "partner_id")] public string PartnerId { get; set; }

        [DataMember(Name = "reference_no")] public string ReferenceNo { get; set; }

        [DataMember(Name = "signature")] public string Signature { get; set; }
    }

    internal class ShopeeReponse
    {
        public string resp_code { get; set; }
        public string resp_msg { get; set; }
        public string balance { get; set; }
        public string reference_no { get; set; }
        public object order_info { get; set; }
        public List<ShopeeItem> cards { get; set; }
        public string signature { get; set; }
    }

    internal class orderInfo
    {
        public int status { get; set; }
        public string remark { get; set; }
        public List<ShopeeItem> cards { get; set; }
    }

    internal class ShopeeItem
    {
        public string serial { get; set; }
        public string pin { get; set; }
        public string expiry { get; set; }
    }
}