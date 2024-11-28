using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Topup.Gw.Model.Dtos;
using Topup.Shared;
using Topup.Shared.Utils;
using Microsoft.Extensions.Logging;
using ServiceStack;

namespace Topup.Gw.Domain.Services;

public class ValidateServiceBase : IValidateServiceBase
{
    private readonly ILogger<ValidateServiceBase> _logger;
    private readonly ISystemService _systemService;

    public ValidateServiceBase(ISystemService systemService, ILogger<ValidateServiceBase> logger)
    {
        _systemService = systemService;
        _logger = logger;
    }

    public async Task<NewMessageResponseBase<PartnerConfigDto>> VerifyPartnerAsync(ValidateRequestDto requestDto)
    {
        try
        {
            _logger.LogInformation($"VerifyPartnerAsync {requestDto.ToJson()}");
            var response = new NewMessageResponseBase<PartnerConfigDto>();
            var partner = await _systemService.GetPartnerCache(requestDto.PartnerCode);
            if (partner == null)
            {
                _logger.LogError($"Tài khoản không tồn tại:{requestDto.ToJson()}");
                response.ResponseStatus =
                    new ResponseStatusApi(ResponseCodeConst.Error, "Giao dịch không thành công");
                return response;
            }

            response.Results = partner;
            if (string.IsNullOrEmpty(partner.ClientId) || string.IsNullOrEmpty(partner.SecretKey))
            {
                _logger.LogInformation(
                    $"Thông tin tài khoản không chính xác hoặc tài khoản không có quyền truy cập:{requestDto.ToJson()}");
                response.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.InvalidAuthen,
                    "Thông tin tài khoản không chính xác hoặc tài khoản không có quyền truy cập");
                return response;
            }

            if (!partner.IsActive)
            {
                _logger.LogInformation($"Tài khoản đang bị khóa:{requestDto.ToJson()}");
                response.ResponseStatus =
                    new ResponseStatusApi(ResponseCodeConst.PartnerNotActive, "Tài khoản đang bị khóa");
                return response;
            }

            if (requestDto.SessionPartnerCode != partner.PartnerCode)
            {
                _logger.LogInformation(
                    $"Thông tin tài khoản không chính xác hoặc tài khoản không có quyền truy cập:{requestDto.ToJson()}");
                response.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.InvalidAuthen,
                    "Thông tin tài khoản không chính xác hoặc tài khoản không có quyền truy cập");
                return response;
            }

            if (partner.EnableSig)
            {
                var check = Cryptography.Verify(requestDto.PlainText, requestDto.Signature?.Replace(" ", "+"),
                    partner.PublicKeyFile);
                if (!check)
                {
                    _logger.LogWarning($"Chữ ký không hợp lệ:{requestDto.ToJson()}");
                    response.ResponseStatus =
                        new ResponseStatusApi(ResponseCodeConst.InvalidSignature, "Chữ ký không hợp lệ");
                    return response;
                }
            }

            if (requestDto.CategoryCode != "CheckTrans")
            {
                var productNotAlows = (partner.ProductConfigsNotAllow ?? "").FromJson<List<string>>();
                if (productNotAlows != null && productNotAlows.Contains(requestDto.ProductCode))
                {
                    _logger.LogInformation(
                        $"Tài khoản không được cấu hình thanh toán cho loại sp {requestDto.ProductCode}:{requestDto.ToJson()}");
                    response.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_ProductNotFound,
                        $"Sản phẩm không được hỗ trợ: {requestDto.ProductCode}");
                    return response;
                }
            }

            if (!string.IsNullOrEmpty(requestDto.ServiceCode))
            {
                var serviceConfig = await _systemService.GetServiceCache(requestDto.ServiceCode);
                if (serviceConfig == null || !serviceConfig.IsActive)
                {
                    _logger.LogInformation($"{requestDto.TransCode}-Dịch vụ {requestDto.ServiceCode} đang tạm khóa");
                    response.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.AccountNotAllowService,
                        $"Dịch vụ {requestDto.ServiceCode} đang tạm khóa");
                    return response;
                }

                var serviceCodes = partner.ServiceConfig.FromJson<List<string>>();
                if (serviceCodes == null || !serviceCodes.Any() || !serviceCodes.Contains(requestDto.ServiceCode))
                {
                    _logger.LogInformation(
                        $"Tài khoản không được cấu hình thanh toán cho dịch vụ {requestDto.ServiceCode}:{requestDto.ToJson()}");
                    response.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.AccountNotAllowService,
                        $"Tài khoản không được cấu hình thanh toán cho dịch vụ {requestDto.ServiceCode}");
                    return response;
                }

                if (requestDto.ServiceCode is ServiceCodes.PIN_CODE or ServiceCodes.PIN_DATA or ServiceCodes.PIN_GAME)
                {
                    if (requestDto.Quantity <= 0)
                    {
                        response.ResponseStatus =
                            new ResponseStatusApi(ResponseCodeConst.Error, "Số lượng yêu cầu không hợp lệ");
                        return response;
                    }

                    if (partner.MaxTotalTrans == 0)
                        partner.MaxTotalTrans = 10;
                    if (requestDto.Quantity > partner.MaxTotalTrans)
                    {
                        _logger.LogInformation(
                            $"Tải khoản không thể mua quá {partner.MaxTotalTrans} thẻ trong giao dịch. Vui lòng liên hệ CSKH để được hỗ trợ");
                        response.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error,
                            $"Tải khoản không thể mua quá {partner.MaxTotalTrans} thẻ trong giao dịch. Vui lòng liên hệ CSKH để được hỗ trợ");
                        return response;
                    }
                }
            }

            if (requestDto.CategoryCode != "CheckTrans")
            {
                var categoryCodes = partner.CategoryConfigs.FromJson<List<string>>();
                if (categoryCodes == null || !categoryCodes.Any() || !categoryCodes.Contains(requestDto.CategoryCode))
                {
                    _logger.LogInformation(
                        $"Tài khoản không được cấu hình thanh toán cho loại sp {requestDto.CategoryCode}:{requestDto.ToJson()}");
                    response.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.AccountNotAllowService,
                        $"Tài khoản không được cấu hình thanh toán cho dịch vụ {requestDto.CategoryCode}");
                    return response;
                }
            }

            response.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Success, "Success");
            return response;
        }
        catch (Exception)
        {
            _logger.LogInformation($"Yêu cầu không hợp lệ. Vui lòng thử lại sau:{requestDto.ToJson()}");
            return new NewMessageResponseBase<PartnerConfigDto>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error,
                    "Yêu cầu không hợp lệ. Vui lòng thử lại sau")
            };
        }
    }
}