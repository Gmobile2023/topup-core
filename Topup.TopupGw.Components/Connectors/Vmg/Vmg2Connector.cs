using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Topup.Shared;
using Topup.Shared.CacheManager;
using Topup.Shared.Dtos;
using Topup.Shared.Helpers;
using Topup.Shared.Utils;
using Topup.TopupGw.Components.Connectors.ESale;
using Topup.TopupGw.Contacts.Dtos;
using Topup.TopupGw.Contacts.Enums;
using Topup.TopupGw.Domains.BusinessServices;
using Topup.TopupGw.Domains.Entities;
using Microsoft.Extensions.Logging;
using ServiceStack;

namespace Topup.TopupGw.Components.Connectors.Vmg2;

public class Vmg2Connector : GatewayConnectorBase
{
    private readonly ILogger<Vmg2Connector> _logger;
    private readonly ITopupGatewayService _topupGatewayService;
    private readonly ICacheManager _cacheManager;

    public Vmg2Connector(ITopupGatewayService topupGatewayService,
        ILogger<Vmg2Connector> logger, ICacheManager cacheManager) : base(topupGatewayService)
    {
        _logger = logger;
        _cacheManager = cacheManager;
        _topupGatewayService = topupGatewayService;
    }

    public override async Task<MessageResponseBase> TopupAsync(TopupRequestLogDto topupRequestLog,
        ProviderInfoDto providerInfo)
    {
        var responseMessage = new MessageResponseBase();
        try
        {
            using (_logger.BeginScope(topupRequestLog.TransCode))
            {
                if (!_topupGatewayService.ValidConnector(ProviderConst.VMG2, providerInfo.ProviderCode))
                {
                    return new MessageResponseBase
                    {
                        ResponseCode = ResponseCodeConst.Error,
                        ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                    };
                }

                var request = new RequestHandle
                {
                    Username = providerInfo.Username,
                    RequestID = topupRequestLog.TransCode,
                    AccountType = "0",
                    TopupAmount = topupRequestLog.TransAmount,
                    TargetAccount = topupRequestLog.ReceiverInfo,
                    ProviderCode = topupRequestLog.Vendor,
                    Operation = 1200,
                };

                if (topupRequestLog.ServiceCode != "TOPUP_DATA")
                {
                    if (request.ProviderCode == "VTE")
                        request.ProviderCode = "Viettel";
                    else if (request.ProviderCode == "VNA")
                        request.ProviderCode = "Vinaphone";
                    else if (request.ProviderCode == "GMOBILE")
                        request.ProviderCode = "Beeline";
                    else if (request.ProviderCode == "VNM")
                        request.ProviderCode = "VNmobile";
                    else if (request.ProviderCode == "VMS")
                        request.ProviderCode = "Mobifone";
                    else if (request.ProviderCode is "WT" or "Wintel")
                        request.ProviderCode = "Reddi";
                }
                else
                {
                    if (request.ProviderCode == "VTE")
                        request.ProviderCode = "DataVTT";
                    else if (request.ProviderCode == "VNA")
                        request.ProviderCode = "DataVNP";
                    else if (request.ProviderCode == "VMS")
                        request.ProviderCode = "DataVMS";
                }


                responseMessage.TransCodeProvider = topupRequestLog.TransCode;
                var result = await CallApi(FunctionVmg.RequestHandle, request, providerInfo);
                if (result == null)
                {
                    _logger.LogWarning(
                        $"Vmg2Connector result is null: {topupRequestLog.ProviderCode}-{topupRequestLog.TransCode}");
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage =
                        "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                    return responseMessage;
                }

                topupRequestLog.ModifiedDate = DateTime.Now;
                topupRequestLog.ResponseInfo = request.ToJson();

                _logger.Log(LogLevel.Information,
                    $"Vmg2Connector return: {topupRequestLog.ProviderCode}-{topupRequestLog.TransCode}-{result.ToJson()}");
                responseMessage.ProviderResponseCode = result?.ErrorCode.ToString();
                responseMessage.ProviderResponseMessage = result?.ErrorMessage;
                if (result.ErrorCode == 0)
                {
                    topupRequestLog.Status = TransRequestStatus.Success;
                    responseMessage.ResponseCode = ResponseCodeConst.Success;
                    responseMessage.ProviderResponseTransCode = result.SysTransId;
                    responseMessage.ResponseMessage = "Giao dịch thành công";
                    if (result.AccRealType != null)
                    {
                        responseMessage.ReceiverType = result.AccRealType switch
                        {
                            0 => "TT",
                            1 => "TS",
                            _ => responseMessage.ReceiverType
                        };
                    }
                }
                else
                {
                    if (result.ErrorCode == -1)
                    {
                        await GetToken(providerInfo, true);
                        responseMessage.ResponseCode = ResponseCodeConst.Error;
                        responseMessage.ResponseMessage = "Giao dịch không thành công";
                    }
                    else
                    {
                        topupRequestLog.Status = TransRequestStatus.Timeout;
                        var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.VMG2,
                            result.ErrorCode.ToString(), topupRequestLog.TransCode);
                        responseMessage.ResponseCode =
                            reResult != null ? reResult.ResponseCode : ResponseCodeConst.ResponseCode_TimeOut;
                        responseMessage.ResponseMessage =
                            reResult != null ? reResult.ResponseName : result.ErrorMessage;
                        if (reResult != null && reResult.ResponseCode is ResponseCodeConst.Error)
                            topupRequestLog.Status = TransRequestStatus.Fail;
                    }
                }

                await _topupGatewayService.TopupRequestLogUpdateAsync(topupRequestLog);

                return responseMessage;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Topup :{topupRequestLog.TransCode} Vmg2Connector Exception: {ex}");
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
            _logger.LogInformation(
                $"{transCodeToCheck}-{transCode}-{providerCode} Vmg2Connector check request: " + transCode);

            var responseMessage = new MessageResponseBase();

            if (providerInfo == null)
                providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(providerCode);

            if (providerInfo == null ||
                !_topupGatewayService.ValidConnector(ProviderConst.VMG2, providerInfo.ProviderCode))
            {
                _logger.LogError(
                    $"{transCodeToCheck}-{transCode}-{providerCode}-Vmg2Connector ProviderConnector not valid");
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult,
                    ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ"
                };
            }

            int operation = 1300;
            if (!string.IsNullOrEmpty(serviceCode) && serviceCode.Contains("PIN"))
                operation = 1100;

            var request = new RequestHandle
            {
                Username = providerInfo.Username,
                RequestID = transCodeToCheck,
                Operation = operation,
                KeyBirthdayTime = operation == 1100 ? providerInfo.PublicKey.Split('|')[0] : null,
            };

            VmgResponse result = await CallApi(FunctionVmg.RequestHandle, request, providerInfo);
            _logger.Log(LogLevel.Information,
                $"{providerCode}-{transCodeToCheck} Vmg2Connector check return: {transCode}-{transCodeToCheck}-{result.ToJson()}");
            if (result.ErrorCode == 0)
            {
                responseMessage.ResponseCode = ResponseCodeConst.Success;
                responseMessage.ResponseMessage = "Giao dịch thành công";
                responseMessage.ProviderResponseTransCode = result.SysTransId;
                if (result.AccRealType != null)
                {
                    responseMessage.ReceiverType = result.AccRealType switch
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
                        var cardList = result.Products.First()
                            .Softpins.Select(card => new CardRequestResponseDto
                            {
                                CardCode = DecryptCodeVmg(card.SoftpinPinCode, providerInfo.PublicKey.Split('|')[1]).EncryptTripDes(),
                                Serial = card.SoftpinSerial,
                                ExpiredDate = DateTime.ParseExact(card.ExpiryDate, "dd/MM/yyyy", CultureInfo.InvariantCulture),
                                ExpireDate = DateTime.ParseExact(card.ExpiryDate, "dd/MM/yyyy", CultureInfo.InvariantCulture).ToString("dd/MM/yyyy"),
                                CardValue = "",//chỗ này sao k có mệnh giá
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
                if (operation == 1100)
                {
                    if (result.ErrorCode is -9)
                    {
                        responseMessage.ResponseCode = ResponseCodeConst.Error;
                        responseMessage.ResponseMessage = "Giao dịch thất bại";
                    }
                    else
                    {
                        responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                        responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                    }

                }
                else
                {
                    if (result.ErrorCode is -7 or -17)
                    {
                        responseMessage.ResponseCode = ResponseCodeConst.Error;
                        responseMessage.ResponseMessage = "Giao dịch thất bại";
                    }
                    else
                    {
                        responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                        responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                    }
                }
            }
            responseMessage.ProviderResponseCode = result?.ErrorCode.ToString();
            responseMessage.ProviderResponseMessage = result?.ErrorMessage;
            return responseMessage;
        }
        catch (Exception e)
        {
            _logger.LogError($"Check_Tran :{transCodeToCheck} Vmg2Connector Exception: {e}");
            return new MessageResponseBase
            {
                ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ",
                ResponseCode = ResponseCodeConst.ResponseCode_TimeOut,
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

    public override async Task<MessageResponseBase> CardGetByBatchAsync(CardRequestLogDto cardRequestLog)
    {
        _logger.LogInformation($"{cardRequestLog.TransCode} Vmg2Connector Get card request: " + cardRequestLog.ToJson());
        var responseMessage = new MessageResponseBase();

        var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(cardRequestLog.ProviderCode);

        if (providerInfo == null)
            return responseMessage;

        if (!_topupGatewayService.ValidConnector(ProviderConst.VMG2, providerInfo.ProviderCode))
        {
            _logger.LogInformation($"{cardRequestLog.ProviderCode}-{cardRequestLog.TransCode}-{providerInfo.ProviderCode}-Vmg2Connector ProviderConnector not valid");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
            };
        }

        var providerService =
            providerInfo.ProviderServices.Find(p => p.ProductCode == cardRequestLog.ProductCode);

        var arrays = new List<BuyItem>();
        arrays.Add(new BuyItem()
        {
            ProductId = Convert.ToInt32(providerService.ServiceCode),
            Quantity = cardRequestLog.Quantity,
        });
        var request = new RequestHandle
        {
            RequestID = cardRequestLog.TransCode,
            Username = providerInfo.Username,
            Operation = 1000,
            BuyItems = arrays,
            KeyBirthdayTime = providerInfo.PublicKey.Split('|')[0],
        };
        responseMessage.TransCodeProvider = cardRequestLog.TransCode;

        _logger.LogInformation($"{cardRequestLog.TransCode} Vmg2Connector Card object send: " + request.ToJson());

        VmgResponse result = await CallApi(FunctionVmg.RequestHandle, request, providerInfo);

        cardRequestLog.ModifiedDate = DateTime.Now;
        _logger.LogInformation($"{cardRequestLog.TransCode} Vmg2Connector Card return: {providerInfo.ProviderCode}-{cardRequestLog.TransRef}-{result.ToJson()}");
        if (result.ErrorCode == 0)
        {
            responseMessage.ResponseCode = ResponseCodeConst.Success;
            responseMessage.ProviderResponseTransCode = result.SysTransId;
            responseMessage.ResponseMessage = "Giao dịch thành công";
            cardRequestLog.Status = TransRequestStatus.Success;
            try
            {
                var cardList = new List<CardRequestResponseDto>();
                foreach (var card in result.Products.First().Softpins)
                {
                    cardList.Add(new CardRequestResponseDto
                    {
                        CardCode = DecryptCodeVmg(card.SoftpinPinCode,
                            providerInfo.PublicKey.Split('|')[1]),
                        Serial = card.SoftpinSerial,
                        ExpiredDate = DateTime.ParseExact(card.ExpiryDate, "dd/MM/yyyy", CultureInfo.InvariantCulture),
                        ExpireDate = DateTime.ParseExact(card.ExpiryDate, "dd/MM/yyyy", CultureInfo.InvariantCulture).ToString("dd/MM/yyyy"),
                        CardValue = cardRequestLog.TransAmount.ToString(CultureInfo.InvariantCulture),
                    });
                }

                responseMessage.Payload = cardList;
            }
            catch (Exception e)
            {
                _logger.LogError($"{cardRequestLog.TransCode} Vmg2Connector Error parsing cards: " + e.Message);
            }
        }
        else
        {
            var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.VMG2,
                result.ErrorCode.ToString(), cardRequestLog.TransCode);
            cardRequestLog.Status = TransRequestStatus.Timeout;
            responseMessage.ResponseCode = reResult != null ? reResult.ResponseCode : ResponseCodeConst.ResponseCode_WaitForResult;
            responseMessage.ResponseMessage = reResult != null ? reResult.ResponseName : result.ErrorMessage;
            responseMessage.ProviderResponseCode = result?.ErrorCode.ToString();
            responseMessage.ProviderResponseMessage = result?.ErrorMessage;
            if (reResult != null && reResult.ResponseCode is ResponseCodeConst.Error)
                cardRequestLog.Status = TransRequestStatus.Fail;
        }

        await _topupGatewayService.CardRequestLogUpdateAsync(cardRequestLog);
        return responseMessage;
    }

    public override async Task<MessageResponseBase> CheckBalanceAsync(string providerCode, string transCode)
    {
        _logger.Log(LogLevel.Information, $"{transCode} Get balance request: " + transCode);

        var responseMessage = new MessageResponseBase();
        var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(providerCode);

        if (providerInfo == null)
            return responseMessage;

        if (!_topupGatewayService.ValidConnector(ProviderConst.VMG2, providerInfo.ProviderCode))
        {
            _logger.LogError($"{transCode}-{providerCode}-Vmg2Connector ProviderConnector not valid");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
            };
        }

        try
        {
            var token = await GetToken(providerInfo);
            var request = new RequestHandle
            {
                Username = providerInfo.Username,
                RequestID = DateTime.Now.ToString("ddmmyyyyhhmmss") + new Random().Next(0, 10),
                Token = token
            };

            VmgResponse result = await CallApi(FunctionVmg.QueryBalance, request, providerInfo);
            _logger.LogInformation($"{transCode} Vmg2Connector Balance : {result?.ToJson()}");
            responseMessage.ResponseCode = ResponseCodeConst.Success;
            responseMessage.ResponseMessage = "Giao dịch thành công";
            responseMessage.Payload = result?.DataValue.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError($"{transCode} -providerCode= {providerCode} Vmg2Connector Balance exception: " + ex.Message);
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
        _logger.LogInformation($"{payBillRequestLog.TransCode} Vmg2Connector paybill request: " + payBillRequestLog.ToJson());

        var responseMessage = new MessageResponseBase();

        var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(payBillRequestLog.ProviderCode);

        if (providerInfo == null)
            return responseMessage;

        if (_topupGatewayService.ValidConnector(ProviderConst.VMG2, providerInfo.ProviderCode))
        {
            _logger.LogError(
                $"{payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{providerInfo.ProviderCode}- Vmg2Connector ProviderConnector not valid");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
            };
        }

        var providerService =
            providerInfo.ProviderServices.Find(p => p.ProductCode == payBillRequestLog.ProductCode);
        var serviceCode = string.Empty;
        if (providerService != null)
            serviceCode = providerService.ServiceCode;
        else
            _logger.LogWarning(
                $"{payBillRequestLog.TransCode} ProviderService with ProductCode [{payBillRequestLog.ProductCode}] is null");
        var request = new RequestHandle
        {
            AccountType = "1",
            Username = providerInfo.Username,
            RequestID = payBillRequestLog.TransCode,
            TargetAccount = payBillRequestLog.ReceiverInfo,
            TopupAmount = Convert.ToInt32(payBillRequestLog.TransAmount),
            ProviderCode = serviceCode,
            Operation = 1200,
        };

        responseMessage.TransCodeProvider = payBillRequestLog.TransCode;
        VmgResponse result = await CallApi(FunctionVmg.RequestHandle, request, providerInfo);
        payBillRequestLog.ModifiedDate = DateTime.Now;
        _logger.LogInformation($"{providerInfo.ProviderCode}-{payBillRequestLog.TransCode} Vmg2Connector Paybill return: {payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{result.ToJson()}");

        if (result.ErrorCode == 0)
        {
            payBillRequestLog.Status = TransRequestStatus.Success;
            responseMessage.ResponseCode = ResponseCodeConst.Success;
            responseMessage.ProviderResponseTransCode = result.SysTransId;
            responseMessage.ResponseMessage = "Giao dịch thành công";
        }
        else
        {
            var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.VMG2,
                result.ErrorCode.ToString(), payBillRequestLog.TransCode);
            payBillRequestLog.Status = TransRequestStatus.Timeout;
            responseMessage.ResponseCode = reResult != null ? reResult.ResponseCode : ResponseCodeConst.ResponseCode_WaitForResult;
            responseMessage.ResponseMessage = reResult != null ? reResult.ResponseName : result.ErrorMessage;
            if (reResult != null && reResult.ResponseCode is ResponseCodeConst.Error)
                payBillRequestLog.Status = TransRequestStatus.Fail;
        }

        responseMessage.ProviderResponseCode = result?.ErrorCode.ToString();
        responseMessage.ProviderResponseMessage = result?.ErrorMessage;
        await _topupGatewayService.PayBillRequestLogUpdateAsync(payBillRequestLog);

        return responseMessage;
    }

    private async Task<VmgResponse> CallApi(string function, RequestHandle request, ProviderInfoDto providerInfo,
        bool isLogin = false)
    {
        VmgResponse result = null;
        try
        {
            string sign;
            if (isLogin)
            {
                sign = Sign(string.Join("|", request.Username, request.MerchantPass),
                    "./" + providerInfo.PrivateKeyFile);
            }
            else
            {
                request.Token = await GetToken(providerInfo);
                sign = Sign(string.Join("|", request.Username, request.RequestID, request.Token,
                    request.Operation), "./" + providerInfo.PrivateKeyFile);
            }

            request.Signature = sign;

            string jsonData = request.ToJson();
            using (_logger.BeginScope("Send request to provider vmg2"))
            {
                _logger.LogInformation("Vmg2Connector request: " + jsonData);
                try
                {
                    ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
                    var client = new JsonServiceClient(providerInfo.ApiUrl)
                    {
                        Timeout = TimeSpan.FromSeconds(providerInfo.Timeout)
                    };
                    result = await client.PostAsync<VmgResponse>(function, jsonData);
                    return result;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"RequestID= {request.RequestID} - Operation= {request.Operation} Vmg2Connector CallApi_vmg2 Exception: " + ex.Message);
                    result = new VmgResponse
                    {
                        ErrorCode = 501102,
                        ErrorMessage = ex.Message
                    };
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"RequestID= {request.RequestID} - Operation= {request.Operation} Vmg2Connector CallApi_vmg2 Exception : " + ex.Message);
            result = new VmgResponse
            {
                ErrorCode = 501102,
                ErrorMessage = ex.Message
            };
        }

        return result;
    }

    private async Task<string> GetToken(ProviderInfoDto providerInfo, bool reLogin = false)
    {
        try
        {
            var key = $"PayGate_ProviderToken:Items:VMG2_TOKEN_{providerInfo.ProviderCode}";
            var tokenCache = await _cacheManager.GetEntity<TokenInfo>(key);
            if (tokenCache != null && !string.IsNullOrEmpty(tokenCache.Token) && reLogin == false)
                return tokenCache.Token;

            var request = new RequestHandle
            {
                Username = providerInfo.Username,
                MerchantPass = providerInfo.Password,
                RequestID = DateTime.Now.ToString("ddmmyyyyhhmmss") + new Random().Next(0, 10),
                Operation = 1400,
            };

            var result = await CallApi(FunctionVmg.RequestHandle, request, providerInfo, true);
            _logger.LogInformation($"Vmg2Connector login return: {result.ToJson()}");
            if (result.ErrorCode != 0 || string.IsNullOrEmpty(result.Token)) return null;
            var token = result.Token;
            var obj = new TokenInfo
            {
                Token = token,
                ProviderCode = providerInfo.ProviderCode,
                RequestDate = DateTime.UtcNow
            };
            await _cacheManager.AddEntity(key, obj, TimeSpan.FromHours(22));
            return token;
        }
        catch (Exception e)
        {
            _logger.LogError($"Vmg2Connector GetToken error:{e}");
            return null;
        }
    }

    private string Sign(string dataToSign, string privateFile)
    {
        var privateKey = File.ReadAllText("files/" + privateFile);
        var privateKeyBlocks = privateKey.Split("-", StringSplitOptions.RemoveEmptyEntries);
        var key = privateKeyBlocks[1].Replace("\r\n", "");
        var privateKeyBytes = Convert.FromBase64String(key);

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

    private static string DecryptCodeVmg(string encryptedSoftpin, string softpinKey)
    {
        try
        {
            using (TripleDESCryptoServiceProvider _3DESCryptoEngine = new TripleDESCryptoServiceProvider())
            {
                byte[] key = GetValidKey(softpinKey, 24);
                byte[] iv = GetValidKey(softpinKey, 8);
                _3DESCryptoEngine.Key = key;
                _3DESCryptoEngine.IV = iv;
                ICryptoTransform decryptor = _3DESCryptoEngine.CreateDecryptor(key, iv);
                byte[] encryptedBytes = Convert.FromBase64String(encryptedSoftpin);
                byte[] decryptedBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
                string result = Encoding.UTF8.GetString(decryptedBytes);
                return result;
            }
        }
        catch (Exception ex)
        {
            return encryptedSoftpin;
        }
    }

    private static byte[] GetValidKey(string key, int size)
    {
        byte[] keyBytes = Encoding.UTF8.GetBytes(key);
        byte[] validKey = new byte[size];
        Array.Copy(keyBytes, validKey, Math.Min(keyBytes.Length, size));
        return validKey;
    }



    internal class BuyItem
    {
        [DataMember(Name = "productId")] public int ProductId { get; set; }
        [DataMember(Name = "quantity")] public int Quantity { get; set; }
    }

    internal class RequestHandle
    {
        [DataMember(Name = "accountType")] public string AccountType { get; set; }
        [DataMember(Name = "buyItems")] public List<BuyItem> BuyItems { get; set; }
        [DataMember(Name = "keyBirthdayTime")] public string KeyBirthdayTime { get; set; }
        [DataMember(Name = "merchantPass")] public string MerchantPass { get; set; }
        [DataMember(Name = "operation")] public int Operation { get; set; }
        [DataMember(Name = "providerCode")] public string ProviderCode { get; set; }
        [DataMember(Name = "requestID")] public string RequestID { get; set; }
        [DataMember(Name = "signature")] public string Signature { get; set; }
        [DataMember(Name = "targetAccount")] public string TargetAccount { get; set; }
        [DataMember(Name = "token")] public string Token { get; set; }
        [DataMember(Name = "topupAmount")] public int? TopupAmount { get; set; }
        [DataMember(Name = "requestTime")] public string RequestTime { get; set; }
        [DataMember(Name = "username")] public string Username { get; set; }
    }

    [DataContract]
    internal class VmgResponse
    {
        [DataMember(Name = "dataValue")] public decimal DataValue { get; set; }

        [DataMember(Name = "errorMessage")] public string ErrorMessage { get; set; }

        [DataMember(Name = "errorCode")] public int ErrorCode { get; set; }

        [DataMember(Name = "merchantBalance")] public decimal MerchantBalance { get; set; }

        [DataMember(Name = "requestID")] public string RequestID { get; set; }

        [DataMember(Name = "sysTransId")] public string SysTransId { get; set; }

        [DataMember(Name = "token")] public string Token { get; set; }

        [DataMember(Name = "accRealType")] public int? AccRealType { get; set; }

        [DataMember(Name = "products")] public List<Product> Products { get; set; }
    }

    [DataContract(Name = "softpin")]
    internal class Softpin
    {
        [DataMember(Name = "softpinSerial")] public string SoftpinSerial { get; set; }

        [DataMember(Name = "softpinPinCode")] public string SoftpinPinCode { get; set; }

        [DataMember(Name = "expiryDate")] public string ExpiryDate { get; set; }
    }

    [DataContract(Name = "product")]
    internal class Product
    {
        [DataMember(Name = "productId")] public int ProductId { get; set; }

        [DataMember(Name = "productValue")] public string ProductValue { get; set; }

        [DataMember(Name = "softpins")] public List<Softpin> Softpins { get; set; }
    }

    internal class FunctionVmg
    {
        public const string RequestHandle = "requestHandle";

        public const string QueryBalance = "queryBalance";
    }
}
