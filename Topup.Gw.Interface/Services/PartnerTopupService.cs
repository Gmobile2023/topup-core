using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Topup.Gw.Model.RequestDtos;
using MassTransit;
using Microsoft.Extensions.Logging;
using Topup.Contracts.Commands.Commons;
using Topup.Contracts.Requests.Commons;
using Topup.Discovery.Requests.Workers;
using Topup.Shared;
using Topup.Shared.Common;
using Topup.Shared.ConfigDtos;
using Topup.Shared.Dtos;
using ServiceStack;
using Topup.Gw.Domain.Services;
using Topup.Gw.Interface.Filters;
using Topup.Gw.Model.Dtos;
using Topup.Shared.AbpConnector;
using Topup.Shared.Helpers;

namespace Topup.Gw.Interface.Services;

[Authenticate]
[PartnerResponse]
public class PartnerTopupService : AppServiceBase
{
    private readonly IBusControl _bus;

    private readonly ILogger<PartnerTopupService> _logger;
    private readonly ISaleService _saleService;
    private readonly GrpcClientHepper _grpcClient;
    private readonly ExternalServiceConnector _externalServiceConnector;
    private readonly IValidateServiceBase _validateServiceBase;

    public PartnerTopupService(IBusControl bus, ILogger<PartnerTopupService> logger, ISaleService saleService,
        GrpcClientHepper grpcClient, ExternalServiceConnector externalServiceConnector,
        IValidateServiceBase validateServiceBase)
    {
        _bus = bus;
        _logger = logger;
        _saleService = saleService;
        _grpcClient = grpcClient;
        _externalServiceConnector = externalServiceConnector;
        _validateServiceBase = validateServiceBase;
    }

    public async Task<object> GetAsync(PartnerCheckTransRequest check)
    {
        var response = new PartnerResponseBase<PartnerResult>(ResponseCodeConst.ResponseCode_WaitForResult,
            "Giao dịch chưa có kết quả. Vui lòng liên hệ CSKH để được hỗ trợ")
        {
            Data = new PartnerResult
            {
                RequestCode = check.RequestCode
            }
        };
        try
        {
            _logger.LogInformation("PartnerCheckTransRequest: {Request}", check.ToJson());

            var validate = await _validateServiceBase.VerifyPartnerAsync(new ValidateRequestDto
            {
                Signature = check.Sig,
                PartnerCode = check.PartnerCode,
                CategoryCode = "CheckTrans",
                PlainText = string.Join("|", check.PartnerCode, check.RequestCode),
                ServiceCode = null,
                SessionPartnerCode = UserSession.AccountCode,
                TransCode = check.RequestCode
            });
            if (validate.ResponseStatus.ErrorCode != ResponseCodeConst.Success)
            {
                _logger.LogInformation("{TransRef}: TopupPartnerRequest not valid: {Response}",
                    check.RequestCode,
                    validate.ToJson());
                response.Status = validate.ResponseStatus.ErrorCode;
                response.Message = validate.ResponseStatus.Message;
                return response;
            }

            var rs = await _saleService.SaleRequestPartnerChecktransAsync(check.RequestCode, check.PartnerCode,
                check.ClientKey);
            response.Status = rs.ResponseCode;
            response.Message = rs.ResponseMessage;
            response.Data.Amount = rs.Payload.Amount;
            response.Data.PaymentAmount = rs.Payload.PaymentAmount;
            response.Data.TransCode = rs.Payload.TransCode;
            response.Data.ProviderType = rs.Payload.ReceiverType;
            response.Data.Discount = rs.Payload.Discount;
            _logger.LogInformation($"PartnerCheckTransRequest:{response.ToJson()}");
            return response;
        }
        catch (Exception e)
        {
            return response;
        }
    }

    public async Task<object> PostAsync(TopupPartnerRequest topupRequest)
    {
        if (topupRequest == null)
            return new PartnerResponseBase<PartnerResult>(ResponseCodeConst.Error, "Invalid request");
        if (string.IsNullOrEmpty(topupRequest.RequestCode) || string.IsNullOrEmpty(topupRequest.PartnerCode) ||
            string.IsNullOrEmpty(topupRequest.PhoneNumber) || string.IsNullOrEmpty(topupRequest.CategoryCode))
            return new PartnerResponseBase<PartnerResult>(ResponseCodeConst.Error, "Invalid request");
        _logger.LogInformation("{Partner}: TopupRequest {Request}", topupRequest.PartnerCode,
            topupRequest.ToJson());
        var response = new PartnerResponseBase<PartnerResult>(ResponseCodeConst.ResponseCode_WaitForResult,
            "Giao dịch chưa có kết quả. Vui lòng liên hệ CSKH để được hỗ trợ")
        {
            Data = new PartnerResult
            {
                RequestCode = topupRequest.RequestCode
            }
        };
        try
        {
            var cate = string.Join("_", topupRequest.CategoryCode, "TOPUP");
            topupRequest.CategoryCode = cate;
            var serviceCode = SaleCommon.GetTopupService(topupRequest.CategoryCode);
            if (serviceCode == null)
            {
                Console.WriteLine($"serviceCode not valid {topupRequest.CategoryCode}");
                response.Status = ResponseCodeConst.ResponseCode_ProductNotFound;
                response.Message = "Thông tin sản phẩm không hợp lệ";
                return response;
            }

            var productCode = SaleCommon.GetProductCode(serviceCode, topupRequest.CategoryCode, topupRequest.Amount);
            if (productCode == null)
            {
                Console.WriteLine(
                    $"productCode not valid {serviceCode} {topupRequest.CategoryCode} {topupRequest.Amount}");
                response.Status = ResponseCodeConst.ResponseCode_ProductNotFound;
                response.Message = "Thông tin sản phẩm không hợp lệ";
            }

            if (topupRequest.PhoneNumber.StartsWith("84"))
                topupRequest.PhoneNumber =
                    Regex.Replace(topupRequest.PhoneNumber, "^84", "0"); //"0" + dto.ReceiverInfo.Substring(2);

            if (!ValidationHelper.IsPhone(topupRequest.PhoneNumber))
            {
                response.Status = ResponseCodeConst.ResponseCode_PhoneNotValid;
                response.Message = "Số điện thoại không hợp lệ";
                return response;
            }


            //Get Product Info
            var productInfo = await _externalServiceConnector.GetProductInfo(topupRequest.RequestCode,
                topupRequest.CategoryCode, productCode, topupRequest.Amount);

            if (productInfo is not { Status: 1 })
            {
                Console.WriteLine(
                    $"productCode not valid {serviceCode} {topupRequest.CategoryCode} {topupRequest.Amount}");
                response.Status = ResponseCodeConst.ResponseCode_ProductNotFound;
                response.Message = "Thông tin sản phẩm không hợp lệ";
            }

            productCode = productInfo.ProductCode;
            var plainText = string.Join("|", topupRequest.PartnerCode, topupRequest.RequestCode,
                topupRequest.PhoneNumber,
                topupRequest.CategoryCode,
                topupRequest.Amount);
            var validate = await _validateServiceBase.VerifyPartnerAsync(new ValidateRequestDto
            {
                Signature = topupRequest.Sig,
                PartnerCode = topupRequest.PartnerCode,
                CategoryCode = topupRequest.CategoryCode,
                PlainText = plainText,
                ServiceCode = serviceCode,
                SessionPartnerCode = UserSession.AccountCode,
                TransCode = topupRequest.RequestCode,
                ProductCode = productCode,
                CheckProductCode = !string.IsNullOrEmpty(productCode)
            });
            if (validate.ResponseStatus.ErrorCode != ResponseCodeConst.Success)
            {
                _logger.LogInformation("{TransRef}: TopupPartnerRequest not valid: {Response}",
                    topupRequest.RequestCode,
                    validate.ToJson());
                response.Status = validate.ResponseStatus.ErrorCode;
                response.Message = validate.ResponseStatus.Message;
                return response;
            }

            var getApi = await _grpcClient.GetClientCluster(GrpcServiceName.Worker).SendAsync(new WorkerTopupRequest
            {
                Amount = topupRequest.Amount,
                Channel = Channel.API,
                AgentType = AgentType.AgentApi,
                AccountType = SystemAccountType.MasterAgent,
                CategoryCode = topupRequest.CategoryCode,
                ProductCode = topupRequest.ProductCode,
                PartnerCode = topupRequest.PartnerCode,
                ReceiverInfo = topupRequest.PhoneNumber,
                RequestIp = Request?.RemoteIp,
                ServiceCode = serviceCode,
                StaffAccount = topupRequest.PartnerCode,
                StaffUser = topupRequest.PartnerCode,
                TransCode = topupRequest.RequestCode,
                RequestDate = DateTime.Now,
                IsCheckReceiverType = validate.Results.IsCheckReceiverType,
                IsCheckPhone = validate.Results.IsCheckPhone,
                IsNoneDiscount = validate.Results.IsNoneDiscount,
                DefaultReceiverType = validate.Results.DefaultReceiverType,
                IsCheckAllowTopupReceiverType = validate.Results.IsCheckAllowTopupReceiverType
            });
            response.Status = getApi.ResponseStatus.ErrorCode;
            response.Message = getApi.ResponseStatus.Message;
            response.Data.Amount = getApi.Results.Amount;
            response.Data.PaymentAmount = getApi.Results.PaymentAmount;
            response.Data.TransCode = getApi.Results.TransRef;
            response.Data.ProviderType = getApi.Results.ReceiverType;
            response.Data.Discount = getApi.Results.Discount;

            _logger.LogInformation("{TransRef}: TopupPartnerRequest response: {Response}", topupRequest.RequestCode,
                response.ToJson());
            return response;
        }
        catch (Exception e)
        {
            _logger.LogError("{TransRef}: TopupPartnerRequest exception: {Exception}", topupRequest.RequestCode, e);
            await _bus.Publish<SendBotMessage>(new
            {
                Message = $"{topupRequest.RequestCode}-{topupRequest.PartnerCode}-TopupPartnerRequest có lỗi: {e}",
                Module = "Gateway",
                MessageType = BotMessageType.Error,
                Title = "TopupPartnerRequest error",
                BotType = BotType.Dev,
                TimeStamp = DateTime.Now,
                CorrelationId = Guid.NewGuid()
            });
            return response;
        }
    }
}