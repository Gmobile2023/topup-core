using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
using Microsoft.Extensions.Logging;
using ServiceStack;

namespace GMB.Topup.TopupGw.Components.Connectors.ESale;

public class ESaleConnector : IGatewayConnector
{
    private readonly ILogger<ESaleConnector> _logger;
    private readonly ITopupGatewayService _topupGatewayService;

    public ESaleConnector(ITopupGatewayService topupGatewayService, ILogger<ESaleConnector> logger)
    {
        _topupGatewayService = topupGatewayService;
        _logger = logger;
    }

    public async Task<MessageResponseBase> TopupAsync(TopupRequestLogDto topupRequestLog, ProviderInfoDto providerInfo)
    {
        throw new NotImplementedException();
    }

    public async Task<MessageResponseBase> TransactionCheckAsync(string providerCode, string transCodeToCheck,
        string transCode, string serviceCode = null, ProviderInfoDto providerInfo = null)
    {
        try
        {
            _logger.LogInformation($"ESaleConnector check request: {transCodeToCheck}-{transCode}-{providerCode}-{serviceCode}");
            var responseMessage = new MessageResponseBase();
            if (providerInfo == null)
                providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(providerCode);

            if (providerInfo == null || !_topupGatewayService.ValidConnector(ProviderConst.ESALE, providerInfo.ProviderCode))
            {
                _logger.LogError($"{transCode}-{providerCode}-{providerCode}-ESaleConnector ProviderConnector not valid");
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult,
                    ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ"
                };
            }
            var request = new CheckTransRequest
            {
                TransId = transCodeToCheck,
                Time = DateTime.Now.ToString("yyMMddHHmmssfff"),
                TransDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                IsGetCard = 1,//Nếu lấy về mã thẻ thì truyền :1, Không cần lấy mã thẻ thì truyền :0
                AgencyCode = providerInfo.ApiUser,
                ClientCode = providerInfo.Username,
            };

            string keySum = string.Join("|", request.AgencyCode, request.TransId, request.IsGetCard, request.Time, providerInfo.ApiPassword);
            string keySignature = string.Join("|", request.AgencyCode, request.TransId, request.IsGetCard, request.Time) + providerInfo.ApiPassword;
            request.CheckSum = HashSHA256(keySum);
            var sign = Sign(keySignature, "./" + providerInfo.PrivateKeyFile);
            request.Signature = sign;

            _logger.LogInformation($"providerCode= {providerCode}|transCode= {transCodeToCheck} ESaleConnector check send: " + request.ToJson());
            var result = await CallApiCheckTransDetail(providerInfo, request);           
            if (result != null)
            {
                _logger.LogInformation($"providerCode= {providerCode}|transCodeToCheck= {transCodeToCheck} ESaleConnector check return: {result.ToJson()}");
                if (result.RetCode == 1)
                {
                    responseMessage.ResponseCode = ResponseCodeConst.Success;
                    responseMessage.ResponseMessage = "Giao dịch thành công";
                    if (request.IsGetCard == 1)
                    {
                        try
                        {
                            var cardList = result.Data.CardsList.Select(card => new CardRequestResponseDto
                            {
                                CardValue = result.Data.unitPrice,
                                CardCode = card.CardCode,
                                Serial = card.Serial,
                                ExpireDate = DateTime.ParseExact(card.ExpiredDate, "dd/MM/yyyy HH:mm:ss",
                                        CultureInfo.InvariantCulture).ToString("dd/MM/yyyy"),                                
                                ExpiredDate = DateTime.ParseExact(card.ExpiredDate, "dd/MM/yyyy HH:mm:ss",
                                        CultureInfo.InvariantCulture)
                            }).ToList();

                            cardList = GenDecryptListCode(providerInfo.PrivateKeyFile, cardList, isTripDes: true);
                            responseMessage.Payload = cardList;
                        }
                        catch (Exception e)
                        {
                            _logger.LogError($"transCodeToCheck= {request.TransId} Error parsing cards: {e.Message}");
                        }
                    }
                }
                else
                {
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                    // var arrayErrors = _topupGatewayService.ConvertArrayCode(providerInfo.ExtraInfo ?? string.Empty);
                    // if (arrayErrors.Contains(result.RetCode))
                    // {
                    //     responseMessage.ResponseCode=ResponseCodeConst.Error;
                    //     responseMessage.ResponseMessage = "Giao dịch không thành công từ nhà cung cấp";
                    // }
                    // else
                    // {
                    //     responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    //     responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                    // }
                }

                responseMessage.ProviderResponseCode = result.RetCode.ToString();
                responseMessage.ProviderResponseMessage = result?.RetMsg;
            }
            else
            {
                _logger.LogInformation($"transCodeToCheck= {transCodeToCheck}|ESaleConnector");
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
            }

            return responseMessage;
        }
        catch (Exception ex)
        {
            _logger.LogError($"transCodeToCheck= {transCodeToCheck} Exception: {ex}");
            return new MessageResponseBase
            {
                ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ",
                ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult
            };
        }
    }

    public async Task<NewMessageResponseBase<InvoiceResultDto>> QueryAsync(PayBillRequestLogDto payBillRequestLog)
    {
        throw new NotImplementedException();
    }

    public async Task<MessageResponseBase> CardGetByBatchAsync(CardRequestLogDto cardRequestLog)
    {
        _logger.Log(LogLevel.Information, $"{cardRequestLog.TransCode} Get card request: " + cardRequestLog.ToJson());
        var responseMessage = new MessageResponseBase();

        var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(cardRequestLog.ProviderCode);

        if (providerInfo == null)
            return responseMessage;

        if (!_topupGatewayService.ValidConnector(ProviderConst.ESALE, providerInfo.ProviderCode))
        {
            _logger.LogError($"{cardRequestLog.TransCode}-{cardRequestLog.TransRef}-{cardRequestLog.ProviderCode}-ESaleConnector ProviderConnector not valid");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
            };
        }

        var product = providerInfo.ProviderServices.Find(p => p.ProductCode == cardRequestLog.ProductCode);
        if (product == null)
        {
            _logger.LogError($"{cardRequestLog.TransCode}-{cardRequestLog.TransRef}-{cardRequestLog.ProviderCode}-ESaleConnector ProviderConnector not config productCode= {cardRequestLog.ProductCode}");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Giao dịch lỗi. Chưa cấu hình sản phẩm"
            };
        }

        int cardId = Convert.ToInt32(product.ServiceCode.Split('|')[0]);
        var request = new CardRequest
        {
            TransId = cardRequestLog.TransCode,
            Time = DateTime.Now.ToString("yyMMddHHmmssfff"),
            TransactionDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            AgencyCode = providerInfo.ApiUser,
            ClientCode = providerInfo.Username,
            CardId = cardId,
            SupplierCode = product.ServiceCode.Split('|')[1],
            Quantity = cardRequestLog.Quantity,

        };

        string keySum = string.Join("|", request.AgencyCode, request.TransId, request.SupplierCode, request.CardId, request.Quantity, request.Time, providerInfo.ApiPassword);
        string keySignature = string.Join("|", request.AgencyCode, request.TransId, request.SupplierCode, request.CardId, request.Quantity, request.Time) + providerInfo.ApiPassword;
        request.checkSum = HashSHA256(keySum);
        var sign = Sign(keySignature, "./" + providerInfo.PrivateKeyFile);
        request.Signature = sign;
        responseMessage.TransCodeProvider = cardRequestLog.TransCode;

        _logger.LogInformation($"TransCode= {cardRequestLog.TransCode} ESaleConnector|Card object send: {request.ToJson()}");

        var result = await CallApiCardCode(providerInfo, request);
        if (result != null)
        {
            cardRequestLog.ModifiedDate = DateTime.Now;
            cardRequestLog.ResponseInfo = result.ToJson();
            _logger.Log(LogLevel.Information, $"ProviderCode= {cardRequestLog.ProviderCode}|TransCode= {cardRequestLog.TransCode} ESaleConnector|Card return: {result.ToJson()}");
            if (result.RetCode == 1)
            {
                responseMessage.ResponseCode = ResponseCodeConst.Success;
                responseMessage.ResponseMessage = "Giao dịch thành công";
                cardRequestLog.Status = TransRequestStatus.Success;
                try
                {
                    var cardList = result.Data.CardsList.Select(card => new CardRequestResponseDto
                    {
                        CardType = cardRequestLog.Vendor,
                        CardValue = (int.Parse(cardRequestLog.ProductCode.Split('_')[2]) * 1000).ToString(),
                        CardCode = card.CardCode,
                        Serial = card.Serial,
                        ExpireDate = DateTime.ParseExact(card.ExpiredDate, "dd/MM/yyyy HH:mm:ss",
                                CultureInfo.InvariantCulture).ToString("dd/MM/yyyy"),
                        //01/09/2026 23:59:59
                        ExpiredDate = DateTime.ParseExact(card.ExpiredDate, "dd/MM/yyyy HH:mm:ss",
                                CultureInfo.InvariantCulture)
                    }).ToList();

                    cardList = GenDecryptListCode(providerInfo.PrivateKeyFile, cardList);
                    responseMessage.Payload = cardList;
                }
                catch (Exception e)
                {
                    _logger.LogError($"TransCode= {cardRequestLog.TransCode} ESaleConnector Error parsing cards: " + e.Message);
                }
            }
            else
            {
                var arrayErrors = _topupGatewayService.ConvertArrayCode(providerInfo.ExtraInfo ?? string.Empty);
                var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.ESALE, result.RetCode.ToString(), cardRequestLog.TransCode);
                if (arrayErrors.Contains(result.RetCode))
                {
                    cardRequestLog.Status = TransRequestStatus.Fail;
                    responseMessage.ResponseCode=reResult != null ? reResult.ResponseCode : ResponseCodeConst.Error;
                    responseMessage.ResponseMessage = reResult != null ? reResult.ResponseName : result.RetMsg;
                }
                else
                {
                    cardRequestLog.Status = TransRequestStatus.Timeout;
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage = reResult != null ? reResult.ResponseName : "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                }
            }
            responseMessage.ProviderResponseCode = result != null ? result.RetCode.ToString() : "";
            responseMessage.ProviderResponseMessage = result?.RetMsg;
        }
        else
        {
            _logger.LogInformation($"TransCode= {cardRequestLog.TransCode} ESaleConnector Error send request");
            responseMessage.ResponseMessage = "Lỗi kết nối nhà cung cấp";
            cardRequestLog.Status = TransRequestStatus.Fail;
        }

        await _topupGatewayService.CardRequestLogUpdateAsync(cardRequestLog);


        return responseMessage;
    }

    public async Task<MessageResponseBase> CheckBalanceAsync(string providerCode, string transCode)
    {
        _logger.LogInformation("Get balance request: " + transCode);
        var responseMessage = new MessageResponseBase();

        var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(providerCode);

        if (providerInfo == null)
            return responseMessage;

        if (!_topupGatewayService.ValidConnector(ProviderConst.ESALE, providerInfo.ProviderCode))
        {
            _logger.LogError($"providerCode= {providerCode}|transCode= {transCode}|ESaleConnector ProviderConnector not valid");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
            };
        }
        var request = new GetBalanceRequest()
        {
            TransId = DateTime.Now.ToString("yyyyMMddHHmmssfff"),
            AgencyCode = providerInfo.ApiUser,
            ClientCode = providerInfo.Username,
            Time = DateTime.Now.ToString("yyMMddHHmmssfff"),
        };
        request.Sig = HashSHA256(string.Join("|", request.TransId, request.AgencyCode, request.Time, providerInfo.ApiPassword));
        _logger.LogInformation($"providerCode= {providerInfo.ProviderCode}|Balance object send: {request.ToJson()}");
        var result = await CallApiBalance(providerInfo, request);
        if (result != null)
        {
            _logger.Log(LogLevel.Information, $"providerCode= {providerCode}|transCode= {transCode}|ESaleConnector Balance return: {result.ToJson()}");
            if (result.RetCode == 1)
            {
                responseMessage.ResponseCode = ResponseCodeConst.Success;
                responseMessage.ResponseMessage = "Giao dịch thành công";
                responseMessage.Payload = result.Data.Balance;
            }
            else
            {
                var arrayErrors = _topupGatewayService.ConvertArrayCode(providerInfo.ExtraInfo ?? string.Empty);
                if (arrayErrors.Contains(result.RetCode))
                {
                    var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.ESALE, result.RetCode.ToString(), request.TransId);
                    responseMessage.ResponseCode = reResult != null ? reResult.ResponseCode : ResponseCodeConst.Error;
                    responseMessage.ResponseMessage = reResult != null ? reResult.ResponseName : result.RetMsg;
                }
                else
                {
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                }
            }
        }
        else
        {
            _logger.LogInformation("ESaleConnector Error send request");
            responseMessage.ResponseMessage = "Lỗi kết nối nhà cung cấp";
        }

        return responseMessage;
    }

    public Task<MessageResponseBase> DepositAsync(DepositRequestDto request)
    {
        throw new NotImplementedException();
    }

    public async Task<MessageResponseBase> PayBillAsync(PayBillRequestLogDto payBillRequestLog)
    {
        throw new NotImplementedException();
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
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        var signature = Convert.ToBase64String(sig);
        return signature;
    }

    private List<CardRequestResponseDto> GenDecryptListCode(string privateFile, List<CardRequestResponseDto> cardList, bool isTripDes = false)
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
                item.CardCode = isTripDes ? stringCode.EncryptTripDes() : stringCode;
            }

            return cardList;
        }
        catch (Exception ex)
        {
            _logger.LogError("GenDecryptListCode exception: " + ex.Message);
            return cardList;
        }
    }

    public Task<ResponseMessageApi<object>> GetProductInfo(GetProviderProductInfo info)
    {
        throw new NotImplementedException();
    }

    public Task<MessageResponseBase> CheckPrePostPaid(string msisdn, string transCode, string providerCode)
    {
        throw new NotImplementedException();
    }

    private async Task<GetBalanceResponse> CallApiBalance(ProviderInfoDto providerInfo, GetBalanceRequest request)
    {
        try
        {
            var client = new JsonServiceClient(providerInfo.ApiUrl)
            {
                Timeout = TimeSpan.FromSeconds(providerInfo.Timeout)
            };

            var res = client.Post<GetBalanceResponse>("getbalance", request.ToJson());
            return res;
        }
        catch (Exception ex)
        {
            _logger.LogError($"TransId= {request.TransId}|Func= getbalance|ESaleConnector_CallApiBalance Exception: {ex}");
            return null;
        }
    }

    private async Task<ESaleResponse> CallApiCardCode(ProviderInfoDto providerInfo, CardRequest request)
    {
        try
        {
            var client = new JsonServiceClient(providerInfo.ApiUrl)
            {
                Timeout = TimeSpan.FromSeconds(providerInfo.Timeout)
            };

            var res = client.Post<ESaleResponse>("buycard", request.ToJson());
            return res;
        }
        catch (Exception ex)
        {
            _logger.LogError($"TransId= {request.TransId}|Func= buycard|ESaleConnector_CallApiCardCode Exception: {ex}");
            return new ESaleResponse()
            {
                RetCode = 501102,
            };
        }
    }

    private async Task<CheckTransResponse> CallApiCheckTransDetail(ProviderInfoDto providerInfo, CheckTransRequest request)
    {
        try
        {
            var client = new JsonServiceClient(providerInfo.ApiUrl)
            {
                Timeout = TimeSpan.FromSeconds(providerInfo.Timeout)
            };

            var res = client.Post<CheckTransResponse>("checktransaction", request.ToJson());
            return res;
        }
        catch (Exception ex)
        {
            _logger.LogError($"TransId= {request.TransId}|Func= checktransaction|ESaleConnector_CallApiCheckTransDetail Exception: {ex}");
            return new CheckTransResponse()
            {
                RetCode = 501102,
            };
        }
    }

    private string HashSHA256(string value)
    {
        StringBuilder Sb = new StringBuilder();
        using (SHA256 hash = SHA256.Create())
        {
            Encoding enc = Encoding.UTF8;
            Byte[] result = hash.ComputeHash(enc.GetBytes(value));
            foreach (Byte b in result)
                Sb.Append(b.ToString("x2"));
        }
        return Sb.ToString();
    }

    private DateTime convertExpiredDate(string date)
    {
        try
        {
            var s = date.Split(' ');
            var d = s[0].Split('/');
            var t = s[1].Split(':');
            return new DateTime(Convert.ToInt16(d[2]), Convert.ToInt16(d[1]), Convert.ToInt16(d[0]), Convert.ToInt16(t[0]), Convert.ToInt16(t[1]), Convert.ToInt16(t[2]));
        }
        catch (Exception ex)
        {
            _logger.LogError($"ExpiredDate= {date}|convertExpiredDate Exception: {ex}");
            return DateTime.Now.AddYears(1);
        }

    }
}