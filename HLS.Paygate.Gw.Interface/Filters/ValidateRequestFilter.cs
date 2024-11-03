using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HLS.Paygate.Gw.Domain.Services;
using HLS.Paygate.Gw.Model.Dtos;
using HLS.Paygate.Gw.Model.RequestDtos;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.AbpConnector;
using HLS.Paygate.Shared.CacheManager;
using HLS.Paygate.Shared.Common;
using HLS.Paygate.Shared.Helpers;
using HLS.Paygate.Shared.Utils;
using ServiceStack;
using ServiceStack.Web;

namespace HLS.Paygate.Gw.Interface.Filters;

public class PartnerFilterAttribute : RequestFilterAsyncAttribute
{
    public override async Task ExecuteAsync(IRequest req, IResponse res, object requestDto)
    {
        var checkResponse = new NewMessageReponseBase<PartnerConfigDto>();
        var validateServiceBase = req.TryResolve<IValidateServiceBase>();
        var externalService = req.TryResolve<ExternalServiceConnector>();
        var session = (CustomUserSession)await req.GetSessionAsync();
        var partnerRequest = string.Empty;
        var plainText = string.Empty;
        var sigText = string.Empty;
        var serviceCode = string.Empty;
        var transCode = string.Empty;
        var cateCode = string.Empty;
        var productCode = string.Empty;

        var quantity = 0;
        var amount = 0;

        var properties = requestDto.GetType().GetProperties();
        if (requestDto.GetType() == typeof(TopupPartnerRequest))
            if (requestDto is TopupPartnerRequest dto)
            {
                transCode = dto.TransCode;
                cateCode = dto.CategoryCode;

                amount = dto.Amount;
                serviceCode = SaleCommon.GetTopupService(dto.CategoryCode);
                if (serviceCode == null)
                {
                    Console.WriteLine($"serviceCode not valid {dto.CategoryCode}");
                    checkResponse.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_ProductNotFound,
                        "Thông tin sản phẩm không hợp lệ")
                    {
                        TransCode = transCode
                    };
                    var newResponse = new NewMessageReponseBase<object>
                    {
                        ResponseStatus = checkResponse.ResponseStatus,
                        Signature = Cryptography.Sign(
                            string.Join("|", checkResponse.ResponseStatus.ErrorCode, transCode),
                            "NT_PrivateKey.pem")
                    };
                    await res.WriteToResponse(req, newResponse);
                    return;
                }

                productCode = SaleCommon.GetProductCode(serviceCode, dto.CategoryCode, dto.Amount);
                if (productCode == null)
                {
                    Console.WriteLine($"productCode not valid {serviceCode} {dto.CategoryCode} {dto.Amount}");
                    checkResponse.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_ProductNotFound,
                        "Thông tin sản phẩm không hợp lệ")
                    {
                        TransCode = transCode
                    };
                    var newResponse = new NewMessageReponseBase<object>
                    {
                        ResponseStatus = checkResponse.ResponseStatus,
                        Signature = Cryptography.Sign(
                            string.Join("|", checkResponse.ResponseStatus.ErrorCode, transCode),
                            "NT_PrivateKey.pem")
                    };
                    await res.WriteToResponse(req, newResponse);
                    return;
                }

                if (dto.ReceiverInfo.StartsWith("84"))
                    dto.ReceiverInfo =
                        Regex.Replace(dto.ReceiverInfo, "^84", "0"); //"0" + dto.ReceiverInfo.Substring(2);

                if (!ValidationHelper.IsPhone(dto.ReceiverInfo))
                {
                    checkResponse.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_PhoneNotValid,
                        "Số điện thoại không hợp lệ")
                    {
                        TransCode = transCode
                    };
                    var newResponse = new NewMessageReponseBase<object>
                    {
                        ResponseStatus = checkResponse.ResponseStatus,
                        Signature = Cryptography.Sign(
                            string.Join("|", checkResponse.ResponseStatus.ErrorCode, transCode),
                            "NT_PrivateKey.pem")
                    };
                    await res.WriteToResponse(req, newResponse);
                    return;
                }

                var productCodeAdd = properties.FirstOrDefault(p => p.Name.Contains("ProductCode"));
                productCodeAdd?.SetValue(requestDto, productCode, null);

                partnerRequest = dto.PartnerCode;
                sigText = dto.Signature;
                plainText = string.Join("|", dto.PartnerCode, dto.TransCode, dto.ReceiverInfo,
                    dto.CategoryCode,
                    dto.Amount);
            }

        if (requestDto.GetType() == typeof(PinCodePartnerRequest))
            if (requestDto is PinCodePartnerRequest dto)
            {
                transCode = dto.TransCode;
                cateCode = dto.CategoryCode;
                amount = dto.CardValue;
                serviceCode = SaleCommon.GetPinCodeService(dto.CategoryCode);
                quantity = dto.Quantity;
                if (serviceCode == null)
                {
                    checkResponse.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_ProductNotFound,
                        "Thông tin sản phẩm không hợp lệ")
                    {
                        TransCode = transCode
                    };
                    var newResponse = new NewMessageReponseBase<object>
                    {
                        ResponseStatus = checkResponse.ResponseStatus,
                        Signature = Cryptography.Sign(
                            string.Join("|", checkResponse.ResponseStatus.ErrorCode, transCode),
                            "NT_PrivateKey.pem")
                    };
                    await res.WriteToResponse(req, newResponse);
                    return;
                }

                productCode = SaleCommon.GetProductCode(serviceCode, dto.CategoryCode, dto.CardValue);
                if (productCode == null)
                {
                    checkResponse.ResponseStatus = new ResponseStatusApi(
                        ResponseCodeConst.ResponseCode_ProductNotFound, "Thông tin sản phẩm không hợp lệ")
                    {
                        TransCode = transCode
                    };
                    var newResponse = new NewMessageReponseBase<object>
                    {
                        ResponseStatus = checkResponse.ResponseStatus,
                        Signature = Cryptography.Sign(
                            string.Join("|", checkResponse.ResponseStatus.ErrorCode, transCode),
                            "NT_PrivateKey.pem")
                    };
                    await res.WriteToResponse(req, newResponse);
                    return;
                }

                var productCodeAdd = properties.FirstOrDefault(p => p.Name.Contains("ProductCode"));
                var serviceCodeAdd = properties.FirstOrDefault(p => p.Name.Contains("ServiceCode"));
                productCodeAdd?.SetValue(requestDto, productCode, null);
                serviceCodeAdd?.SetValue(requestDto, serviceCode, null);

                partnerRequest = dto.PartnerCode;
                sigText = dto.Signature;
                dto.ServiceCode = serviceCode;
                plainText = string.Join("|", dto.PartnerCode, dto.TransCode, dto.CategoryCode,
                    dto.CardValue, dto.Quantity);
            }

        if (requestDto.GetType() == typeof(PayBillPartnerRequest))
            if (requestDto is PayBillPartnerRequest dto)
            {
                amount = (int)dto.Amount;
                transCode = dto.TransCode;
                cateCode = dto.CategoryCode;
                partnerRequest = dto.PartnerCode;
                sigText = dto.Signature;
                serviceCode = ServiceCodes.PAY_BILL;
                plainText = string.Join("|", dto.PartnerCode, dto.TransCode, dto.ReceiverInfo,
                    dto.CategoryCode, dto.ProductCode, dto.Amount);
                productCode = dto.ProductCode;
            }

        if (requestDto.GetType() == typeof(BillQueryPartnerRequest))
            if (requestDto is BillQueryPartnerRequest dto)
            {
                transCode = dto.TransCode;
                cateCode = dto.CategoryCode;
                partnerRequest = dto.PartnerCode;
                sigText = dto.Signature;
                serviceCode = ServiceCodes.QUERY_BILL;
                plainText = string.Join("|", dto.PartnerCode, dto.ReceiverInfo, dto.CategoryCode,
                    dto.ProductCode);
                productCode = dto.ProductCode;
            }

        if (requestDto.GetType() == typeof(CheckTransAuthenRequest))
            if (requestDto is CheckTransAuthenRequest dto)
            {
                transCode = dto.TransCode;
                cateCode = "CheckTrans";
                serviceCode = null;
                partnerRequest = dto.PartnerCode;
                sigText = dto.Signature;
                plainText = string.Join("|", dto.PartnerCode, dto.TransCode, dto.TransCodeToCheck);
            }

        if (requestDto.GetType() == typeof(CheckTransAuthenV2Request))
            if (requestDto is CheckTransAuthenV2Request dto)
            {
                transCode = dto.TransCode;
                cateCode = "CheckTrans";
                serviceCode = null;
                partnerRequest = dto.PartnerCode;
                sigText = dto.Signature;
                plainText = string.Join("|", dto.PartnerCode, dto.TransCode, dto.TransCodeToCheck);
            }
        if (requestDto.GetType() == typeof(CheckTransAuthenNewRequest))
            if (requestDto is CheckTransAuthenNewRequest dto)
            {
                transCode = dto.TransCode;
                cateCode = "CheckTrans";
                serviceCode = null;
                partnerRequest = dto.PartnerCode;
                sigText = dto.Signature;
                plainText = string.Join("|", dto.PartnerCode, dto.TransCode, dto.TransCodeToCheck);
            }

        //Chỉ check với  Topup,
        if (!string.IsNullOrEmpty(productCode) && requestDto.GetType() == typeof(TopupPartnerRequest))
        {
            //Get Product Info
            var productInfo = await externalService.GetProductInfo(transCode, cateCode, productCode, amount);

            if (productInfo == null || productInfo.Status != 1)
            {
                Console.WriteLine($"productInfo null {serviceCode} {cateCode} {productCode} {amount}");
                checkResponse.ResponseStatus = new ResponseStatusApi(
                    ResponseCodeConst.ResponseCode_ProductNotFound, "Thông tin sản phẩm không hợp lệ")
                {
                    TransCode = transCode
                };
                var newResponse = new NewMessageReponseBase<object>
                {
                    ResponseStatus = checkResponse.ResponseStatus,
                    Signature = Cryptography.Sign(
                        string.Join("|", checkResponse.ResponseStatus.ErrorCode, transCode),
                        "NT_PrivateKey.pem")
                };
                await res.WriteToResponse(req, newResponse);
                return;
            }
            //Cache lại thông tin này để Worker lấy lại xử lý.
            //await cacheManager.AddEntity<ProductInfoDto>($"PayGates_ProductInfos:Items:{partnerRequest}:{productCode}:{transCode}-{amount}", productInfo,600);
            //if (productInfo.ProductCode != productCode)
            //{
            //    productCode = productInfo.ProductCode;
            //}

            productCode = productInfo.ProductCode;
        }

        checkResponse = await validateServiceBase.VerifyPartnerAsync(new ValidateRequestDto
        {
            Signature = sigText,
            PartnerCode = partnerRequest,
            CategoryCode = cateCode,
            PlainText = plainText,
            ServiceCode = serviceCode,
            SessionPartnerCode = session.AccountCode,
            TransCode = transCode,
            Quantity = quantity,
            ProductCode = productCode,
            CheckProductCode = !string.IsNullOrEmpty(productCode)
        });
        if (checkResponse.ResponseStatus.ErrorCode != ResponseCodeConst.Success)
        {
            // if (checkResponse.ResponseStatus.ErrorCode is ResponseCodeConst.InvalidSignature
            //     or ResponseCodeConst.InvalidAuthen)
            // {
            //     var errorResponse = DtoUtils.CreateErrorResponse(checkResponse.ResponseStatus.ErrorCode,
            //         checkResponse.ResponseStatus.Message, null);
            //     await res.WriteToResponse(req, errorResponse);
            //     return;
            // }
            // else
            // {
            //     var newResponse = new NewMessageReponseBase<object>
            //     {
            //         ResponseStatus = checkResponse.ResponseStatus, Signature = Cryptography.Sign(
            //             string.Join("|", checkResponse.ResponseStatus.ErrorCode, transCode),
            //             "NT_PrivateKey.pem")
            //     };
            //     await res.WriteToResponse(req, newResponse);
            //     return;
            // }
            checkResponse.ResponseStatus.TransCode = transCode;
            var newResponse = new NewMessageReponseBase<object>
            {
                ResponseStatus = checkResponse.ResponseStatus,
                Signature = Cryptography.Sign(string.Join("|", checkResponse.ResponseStatus.ErrorCode, transCode),
                    "NT_PrivateKey.pem")
            };
            await res.WriteToResponse(req, newResponse);
            return;
        }

        var clientKeyAdd = properties.FirstOrDefault(p => p.Name.Contains("ClientKey"));
        clientKeyAdd?.SetValue(requestDto, checkResponse.Results.SecretKey, null);

        var addCheck = properties.FirstOrDefault(p => p.Name.Contains("IsCheckReceiverType"));
        addCheck?.SetValue(requestDto, checkResponse.Results.IsCheckReceiverType, null);

        var addCheckPhone = properties.FirstOrDefault(p => p.Name.Contains("IsCheckPhone"));
        addCheckPhone?.SetValue(requestDto, checkResponse.Results.IsCheckPhone, null);

        var isNonDiscount = properties.FirstOrDefault(p => p.Name.Contains("IsNoneDiscount"));
        isNonDiscount?.SetValue(requestDto, checkResponse.Results.IsNoneDiscount, null);

        var isCheckAllowTopupReceiverType =
            properties.FirstOrDefault(p => p.Name.Contains("IsCheckAllowTopupReceiverType"));
        isCheckAllowTopupReceiverType?.SetValue(requestDto, checkResponse.Results.IsCheckAllowTopupReceiverType, null);

        var defaultReceiverType = properties.FirstOrDefault(p => p.Name.Contains("DefaultReceiverType"));
        defaultReceiverType?.SetValue(requestDto, checkResponse.Results.DefaultReceiverType, null);
    }
}