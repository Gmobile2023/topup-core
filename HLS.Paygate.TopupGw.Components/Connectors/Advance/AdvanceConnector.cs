using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.Utils;
using HLS.Paygate.TopupGw.Contacts.Dtos;
using HLS.Paygate.TopupGw.Contacts.Enums;
using HLS.Paygate.TopupGw.Domains.BusinessServices;
using Microsoft.Extensions.Logging;
using ServiceStack;

namespace HLS.Paygate.TopupGw.Components.Connectors.Advance
{
    public class AdvanceConnector : GatewayConnectorBase
    {
        private readonly ILogger<AdvanceConnector> _logger;

        public AdvanceConnector(ITopupGatewayService topupGatewayService, ILogger<AdvanceConnector> logger) : base(
            topupGatewayService)
        {
            _logger = logger;
        }

        public override async Task<MessageResponseBase> TopupAsync(TopupRequestLogDto topupRequestLog,
            ProviderInfoDto providerInfo)
        {
            _logger.LogInformation("{TransCode} AdvanceConnector topup request: {Log}", topupRequestLog.TransCode,
                topupRequestLog.ToJson());
            var responseMessage = new MessageResponseBase();
            try
            {
                if (!TopupGatewayService.ValidConnector(ProviderConst.ADVANCE, providerInfo.ProviderCode))
                {
                    _logger.LogError(
                        "{TransCode}-{TransRef}-{ProviderCode}-AdvanceConnector ProviderConnector not valid",
                        topupRequestLog.TransCode, topupRequestLog.TransRef, providerInfo.ProviderCode);
                    return new MessageResponseBase
                    {
                        ResponseCode = ResponseCodeConst.Error,
                        ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                    };
                }

                var telcoCode = topupRequestLog.Vendor switch
                {
                    "VMS" => "MBF_TU",
                    "VNA" => "VNP_TU",
                    "VTE" => "VTT_TU",
                    "VNM" => "VNMB_TU",
                    _ => string.Empty
                };

                var data = new RequestApiObject
                {
                    type_card = telcoCode,
                    amount = topupRequestLog.TransAmount,
                    time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    account_api = providerInfo.ApiUser,
                    phone = topupRequestLog.ReceiverInfo,
                    request_id = topupRequestLog.TransCode
                };
                data.content_drap = $"{telcoCode}{data.phone}{data.amount}{data.time}";
                var plainText = string.Join("", data.account_api.Md5(), data.content_drap.Md5(),
                    providerInfo.ApiPassword.Md5());
                data.sign = plainText.Md5();

                var input = new List<KeyValuePair<string, string>>()
                {
                    new("type_card", data.type_card),
                    new("phone", data.phone),
                    new("request_id", data.request_id),
                    new("amount", data.amount.ToString()),
                    new("time", data.time),
                    new("account_api", data.account_api),
                    new("sign", data.sign),
                };

                responseMessage.TransCodeProvider = topupRequestLog.TransCode;
                _logger.LogInformation("{TransCode} AdvanceConnector send: {Data}", topupRequestLog.TransCode,
                    data.ToJson());
                var result = await CallApi(providerInfo.ApiUrl + "/card/topup", input, topupRequestLog.TransCode,
                    providerInfo.Timeout);
                _logger.LogInformation("AdvanceConnector CallApi return: {TransCode}-{TransRef}-{Return}",
                    topupRequestLog.TransCode,
                    topupRequestLog.TransRef, result.ToJson());

                if (result != null)
                {
                    topupRequestLog.ResponseInfo = result.ToJson();
                    topupRequestLog.ModifiedDate = DateTime.Now;
                    responseMessage.ProviderResponseCode = result.status.ToString();
                    responseMessage.ProviderResponseMessage = result.message;
                    _logger.LogInformation("{ProviderCode}{TransCode} AdvanceConnector return: {TransRef}-{Json}",
                        topupRequestLog.ProviderCode, topupRequestLog.TransCode, topupRequestLog.TransRef,
                        result.ToJson());

                    if (result.error_code == "200")
                    {
                        if (result.status == 1)
                        {
                            topupRequestLog.Status = TransRequestStatus.Success;
                            responseMessage.ProviderResponseTransCode = string.Empty;
                            responseMessage.ReceiverType = "";
                            responseMessage.ResponseCode = ResponseCodeConst.Success;
                            responseMessage.ResponseMessage = "Giao dịch thành công";
                        }
                        else
                        {
                            var arrayErrors =
                                TopupGatewayService.ConvertArrayCode(providerInfo.ExtraInfo ?? string.Empty);
                            if (arrayErrors.Contains(result.status))
                            {
                                topupRequestLog.Status = TransRequestStatus.Fail;
                                responseMessage.ResponseCode = ResponseCodeConst.Error;
                                responseMessage.ResponseMessage = result.message;
                            }
                            else
                            {
                                topupRequestLog.Status = TransRequestStatus.Timeout;
                                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                                responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả xử lý.";
                            }
                        }
                    }
                    else
                    {
                        var reResult = await TopupGatewayService.GetResponseMassageCacheAsync(ProviderConst.ADVANCE,
                            result.error_code, topupRequestLog.TransCode);
                        topupRequestLog.Status = TransRequestStatus.Timeout;
                        responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                        responseMessage.ResponseMessage =
                            reResult != null ? reResult.ReponseName : "Giao dịch đang chờ kết quả xử lý.";
                    }
                }
                else
                {
                    _logger.LogInformation($"{topupRequestLog.TransCode} Error send request");
                    responseMessage.ResponseMessage = "Lỗi kết nối nhà cung cấp";
                    topupRequestLog.Status = TransRequestStatus.Fail;
                }

                await TopupGatewayService.TopupRequestLogUpdateAsync(topupRequestLog);
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

        public override async Task<MessageResponseBase> TransactionCheckAsync(string providerCode,
            string transCodeToCheck,
            string transCode, string serviceCode = null,
            ProviderInfoDto providerInfo = null)
        {
            _logger.LogInformation("{TransCodeToCheck} IrisConnector check request: {TransCode}", transCodeToCheck,
                transCode);
            var responseMessage = new MessageResponseBase();
            try
            {
                if (providerInfo == null)
                    providerInfo = await TopupGatewayService.ProviderInfoCacheGetAsync(providerCode);


                if (providerInfo == null ||
                    !TopupGatewayService.ValidConnector(ProviderConst.ADVANCE, providerInfo.ProviderCode))
                {
                    _logger.LogError("{TransCode}-{ProviderCode}- AdvanceConnector ProviderConnector not valid",
                        transCode,
                        providerCode);
                    return new MessageResponseBase
                    {
                        ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult,
                        ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ"
                    };
                }

                _logger.LogInformation("Trans Check object send: {Trans}", transCodeToCheck);
                var data = new RequestApiObject
                {
                    trans_id = transCodeToCheck,
                    time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    account_api = providerInfo.ApiUser,
                };
                data.content_drap = $"{data.trans_id}{data.time}";
                var plainText = string.Join("", data.account_api.Md5(), data.content_drap.Md5(),
                    providerInfo.ApiPassword.Md5());
                data.sign = plainText.Md5();

                var input = new List<KeyValuePair<string, string>>()
                {
                    new KeyValuePair<string, string>("trans_id", data.trans_id),
                    new KeyValuePair<string, string>("time", data.time),
                    new KeyValuePair<string, string>("account_api", data.account_api),
                    new KeyValuePair<string, string>("sign", data.sign),
                };


                var result = await CallApi(providerInfo.ApiUrl + "/card/get_detail_trans", input, transCodeToCheck,
                    providerInfo.Timeout);
                _logger.LogInformation("AdvanceConnector CallApi return: {TransCode}-{TransRef}-{Return}",
                    transCodeToCheck, transCode, result.ToJson());
                if (result != null)
                {
                    _logger.LogInformation(
                        $"{providerCode}-{transCodeToCheck}  AdvanceConnector Check trans return: {transCodeToCheck}-{transCode}-{result.ToJson()}");

                    if (result.error_code == "200")
                    {
                        if (result.status == 1)
                        {
                            responseMessage.ResponseCode = ResponseCodeConst.Success;
                            responseMessage.ResponseMessage = "Giao dịch thành công";
                            responseMessage.ProviderResponseTransCode = string.Empty;
                        }
                        else
                        {
                            var arrayErrors =
                                TopupGatewayService.ConvertArrayCode(providerInfo.ExtraInfo ?? string.Empty);
                            if (arrayErrors.Contains(result.status))
                            {
                                responseMessage.ResponseCode = ResponseCodeConst.Error;
                                responseMessage.ResponseMessage = "Giao dịch không thành công từ nhà cung cấp";
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
                    }
                    else
                    {
                        var reResult = await TopupGatewayService.GetResponseMassageCacheAsync(ProviderConst.ADVANCE,
                            result.error_code, transCodeToCheck);
                        responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                        responseMessage.ResponseMessage = reResult != null ? reResult.ReponseName : result.error_code;
                        responseMessage.ProviderResponseCode = result?.error_code;
                        responseMessage.ProviderResponseMessage = result?.message;
                    }
                }
                else
                {
                    _logger.LogInformation("{TransCodeToCheck} Error send request", transCodeToCheck);
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

        public override async Task<MessageResponseBase> CheckBalanceAsync(string providerCode, string transCode)
        {
            _logger.LogInformation("{TransCode} Get balance request", transCode);
            var responseMessage = new MessageResponseBase();
            var providerInfo = await TopupGatewayService.ProviderInfoCacheGetAsync(providerCode);

            if (providerInfo == null)
                return responseMessage;

            if (!TopupGatewayService.ValidConnector(ProviderConst.ADVANCE, providerInfo.ProviderCode))
            {
                _logger.LogError("{ProviderCode}-{TransCode}-AdvanceConnector ProviderConnector not valid",
                    providerCode,
                    transCode);
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                };
            }

            var data = new RequestApiObject
            {
                username = providerInfo.ApiUser,
                time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                account_api = providerInfo.ApiUser,
            };
            data.content_drap = $"{data.username}{data.time}";
            var plainText = string.Join("", data.account_api.Md5(), data.content_drap.Md5(),
                providerInfo.ApiPassword.Md5());
            data.sign = plainText.Md5();

            var input = new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>("username", data.username),
                new KeyValuePair<string, string>("time", data.time),
                new KeyValuePair<string, string>("account_api", data.account_api),
                new KeyValuePair<string, string>("sign", data.sign),
            };
            _logger.LogInformation("{TransCode} Balance object send: {Data}", transCode, data.ToJson());
            var result = await CallApi(providerInfo.ApiUrl + "/agent/get_info", input, transCode,
                providerInfo.Timeout);

            if (result != null && result.error_code == "200")
            {
                _logger.LogInformation($"{transCode} Balance return: {transCode}-{result.ToJson()}");
                if (result.status == 1)
                {
                    responseMessage.ResponseCode = ResponseCodeConst.Success;
                    responseMessage.ResponseMessage = "Giao dịch thành công";
                    responseMessage.Payload = result.balance.ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    responseMessage.ResponseCode = ResponseCodeConst.Error;
                    responseMessage.ResponseMessage = result.message;
                }
            }
            else
            {
                _logger.LogInformation("{TransCode} Error send request", transCode);
                responseMessage.ResponseMessage = "Lỗi kết nối nhà cung cấp";
            }

            return responseMessage;
        }

        public override async Task<MessageResponseBase> CardGetByBatchAsync(CardRequestLogDto cardRequestLog)
        {
            _logger.LogInformation($"{cardRequestLog.TransCode} AdvanceConnector card request: " +
                                   cardRequestLog.ToJson());
            var responseMessage = new MessageResponseBase();
            var providerInfo = await TopupGatewayService.ProviderInfoCacheGetAsync(cardRequestLog.ProviderCode);

            if (providerInfo == null)
                return responseMessage;

            if (!TopupGatewayService.ValidConnector(ProviderConst.ADVANCE, providerInfo.ProviderCode))
            {
                _logger.LogError(
                    $"{cardRequestLog.TransCode}-{cardRequestLog.TransRef}-{providerInfo.ProviderCode}-AdvanceConnector ProviderConnector not valid");
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                };
            }


            var telcoCode = cardRequestLog.Vendor switch
            {
                "VMS" => "MBF_BC",
                "VNP" => "VNP_BC",
                "VTE" => "VTT_BC",
                _ => string.Empty
            };

            var data = new RequestApiObject
            {
                type_card = telcoCode,
                amount = cardRequestLog.TransAmount,
                total = cardRequestLog.Quantity,
                time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                account_api = providerInfo.ApiUser,
                request_id = cardRequestLog.TransCode
            };
            data.content_drap = $"{telcoCode}{data.phone}{data.total}{data.amount}{data.time}";
            var plainText = string.Join("", data.account_api.Md5(), data.content_drap.Md5(),
                providerInfo.ApiPassword.Md5());
            data.sign = plainText.Md5();

            var input = new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>("type_card", data.type_card),
                new KeyValuePair<string, string>("total", data.total.ToString()),
                new KeyValuePair<string, string>("request_id", data.request_id),
                new KeyValuePair<string, string>("amount", data.amount.ToString()),
                new KeyValuePair<string, string>("time", data.time),
                new KeyValuePair<string, string>("account_api", data.account_api),
                new KeyValuePair<string, string>("sign", data.sign),
            };

            responseMessage.TransCodeProvider = cardRequestLog.TransCode;
            _logger.LogInformation("{TransCode} AdvanceConnector send: {Data}", cardRequestLog.TransCode,
                data.ToJson());
            var result = await CallApi(providerInfo.ApiUrl + "/card/buycode", input, cardRequestLog.TransCode,
                providerInfo.Timeout);
            _logger.LogInformation("AdvanceConnector CallApi return: {TransCode}-{TransRef}-{Return}",
                cardRequestLog.TransCode,
                cardRequestLog.TransRef, result.ToJson());

            if (result != null)
            {
                cardRequestLog.ResponseInfo = ""; //result.ToJson();
                cardRequestLog.ModifiedDate = DateTime.Now;
                responseMessage.ProviderResponseCode = result.status.ToString();
                responseMessage.ProviderResponseMessage = result.message;
                _logger.LogInformation("{ProviderCode}{TransCode} AdvanceConnector return: {TransRef}-{Json}",
                    cardRequestLog.ProviderCode, cardRequestLog.TransCode, cardRequestLog.TransRef, result.ToJson());

                if (result.error_code == "200")
                {
                    if (result.status == 1)
                    {
                        cardRequestLog.Status = TransRequestStatus.Success;
                        responseMessage.ProviderResponseTransCode = string.Empty;
                        responseMessage.ReceiverType = "";
                        responseMessage.ResponseCode = ResponseCodeConst.Success;
                        responseMessage.ResponseMessage = "Giao dịch thành công";
                    }
                    else
                    {
                        var arrayErrors = TopupGatewayService.ConvertArrayCode(providerInfo.ExtraInfo ?? string.Empty);
                        if (arrayErrors.Contains(result.status))
                        {
                            cardRequestLog.Status = TransRequestStatus.Fail;
                            responseMessage.ResponseCode = ResponseCodeConst.Error;
                            responseMessage.ResponseMessage = result.message;
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
                    var reResult = await TopupGatewayService.GetResponseMassageCacheAsync(ProviderConst.ADVANCE,
                        result.error_code, cardRequestLog.TransCode);
                    cardRequestLog.Status = TransRequestStatus.Timeout;
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage =
                        reResult != null ? reResult.ReponseName : "Giao dịch đang chờ kết quả xử lý.";
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

        private async Task<ResponseObject> CallApi(string url, List<KeyValuePair<string, string>> data,
            string transCode, int timeout = 0)
        {
            var responseString = string.Empty;
            var exception = string.Empty;
            var retryCount = 0;
            do
            {
                try
                {
                    var client = new HttpClient();
                    var reponseData = await client.PostAsync(url, new FormUrlEncodedContent(data));
                    responseString = reponseData.ReadToEnd();
                    _logger.LogInformation("Advance callapi response {TransCode}-{ResponseString}", transCode,
                        responseString);
                    retryCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError("Trans exception: {Ex}", ex.Message);
                    exception = ex.Message;
                    responseString = "TIMEOUT";
                }
            } while (string.IsNullOrEmpty(responseString) && retryCount < 3);

            if (!string.IsNullOrEmpty(responseString))
            {
                if (responseString == "TIMEOUT")
                    return new ResponseObject
                    {
                        error_code = "501102",
                        message = exception
                    };

                var responseMessage = responseString.FromJson<ResponseObject>();
                return responseMessage;
            }

            return new ResponseObject
            {
                error_code = "501102",
                message = "Send request timeout!"
            };
        }

        private static string TripleDesDecrypt(string Key, string cypherText)
        {
            var des = CreateDes(Key);
            var ct = des.CreateDecryptor();
            var input = Convert.FromBase64String(cypherText);
            var output = ct.TransformFinalBlock(input, 0, input.Length);
            return Encoding.UTF8.GetString(output);
        }

        private static TripleDES CreateDes(string key)
        {
            MD5 md5 = MD5.Create();
            TripleDES des = TripleDES.Create();
            var desKey = md5.ComputeHash(Encoding.UTF8.GetBytes(key));
            des.Key = desKey;
            des.IV = new byte[des.BlockSize / 8];
            des.Padding = PaddingMode.PKCS7;
            des.Mode = CipherMode.ECB;
            return des;
        }


        private class RequestApiObject
        {
            public string trans_id { get; set; }
            public string request_id { get; set; }
            public string username { get; set; }
            public string type_card { get; set; }
            public string phone { get; set; }
            public decimal amount { get; set; }
            public string time { get; set; }
            public string account_api { get; set; }
            public string content_drap { get; set; }
            public int total { get; set; }
            public string sign { get; set; }
        }

        public class ResponseObject
        {
            public int status { get; set; }
            public string trans_id { get; set; }
            public decimal balance { get; set; }
            public string error_code { get; set; }
            public string message { get; set; }
            public string data_card { get; set; }
        }
    }
}