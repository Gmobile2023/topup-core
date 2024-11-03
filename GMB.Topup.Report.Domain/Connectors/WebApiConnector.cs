using GMB.Topup.Report.Model.Dtos.RequestDto;
using GMB.Topup.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ServiceStack;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GMB.Topup.Discovery.Requests.Balance;
using GMB.Topup.Report.Domain.Entities;
using GMB.Topup.Report.Model.Dtos;

namespace GMB.Topup.Report.Domain.Connectors
{
    public class WebApiConnector
    {
        private readonly ILogger<WebApiConnector> _logger;

        private readonly string _apiUrl;
        private readonly string _apiUrlTopup;
        private readonly string _apiUrlBalance;

        public WebApiConnector(IConfiguration configuration, ILogger<WebApiConnector> logger)
        {
            _logger = logger;
            _apiUrl = configuration["ServiceConfig:GatewayPrivate"];
            _apiUrlTopup = configuration["ServiceConfig:GatewayTopup"];
            _apiUrlBalance = configuration["ServiceConfig:BalancePrivate"];
            
        }

        public async Task<ProductInfoDto> GetProductInfoAsync(string productCode)
        {
            try
            {
                _logger.LogInformation($"GetProductInfoAsync request:{productCode}");
                ProductInfoDto use = null;

                using var client = new JsonServiceClient(_apiUrl);
                var result = await client.GetAsync<ResponseMessageApi<ProductInfoDto>>(new GetProductInfoRequest
                {
                    ProductCode = productCode
                });
                _logger.LogInformation($"GetProductInfoAsync response:{result.Success}-{result.Result.ToJson()}");
                if (result.Success)
                {
                    use = result.Result;
                }

                return use;
            }
            catch (Exception e)
            {
                _logger.LogError($"GetProductInfoAsync error: {e}");
                return null;
            }
        }

        public async Task<UserInfoDto> GetUserInfoDtoAsync(string accountCode, int userId = 0)
        {
            try
            {
                _logger.LogInformation($"GetUserInfoDtoAsync request:{accountCode}");
                UserInfoDto use = null;

                using var client = new JsonServiceClient(_apiUrl);
                ResponseMessageApi<UserInfoDto> result = await client.GetAsync<ResponseMessageApi<UserInfoDto>>(new GetUserInfoQueryRequest
                {
                    AccountCode = accountCode,
                    UserId = userId,
                    AgentType = 99
                });
                //_logger.LogInformation($"GetUserInfoDtoAsync response:{result.Success}-{result.Result.ToJson()}");
                if (result.Success)
                {
                    use = result.Result;
                }

                return use;
            }
            catch (Exception e)
            {
                _logger.LogError($"GetUserInfoDtoAsync error: {e}");
                return null;
            }
        }

        public async Task<List<UserInfoPeriodDto>> GetUserPeriodAsync(string agentCode, AgentType agentType)
        {
            try
            {
                _logger.LogInformation("GetUserPeriodAsync request:{agentCode}");
                using var client = new JsonServiceClient(_apiUrl);

                var check = await client.GetAsync<ResponseMessageApi<string>>(new GetUserPeriodRequest
                {
                    AgentCode = agentCode,
                    AgentType = agentType
                });
                _logger.LogInformation($"GetUserPeriodAsync response:{check.Result}");
                var use = check.Result.FromJson<List<UserInfoPeriodDto>>();
                return use;
            }
            catch (Exception e)
            {
                _logger.LogError($"GetUserPeriodAsync error: {e}");
                return null;
            }
        }

        public async Task<ServiceInfoDto> GetServiceInfoDtoAsync(string serviceCode)
        {
            try
            {
                _logger.LogInformation($"GetServiceInfoDtoAsync request:{serviceCode}");
                ServiceInfoDto use = null;

                using var client = new JsonServiceClient(_apiUrl);
                ResponseMessageApi<ServiceInfoDto> result = await client.GetAsync<ResponseMessageApi<ServiceInfoDto>>(new GetServiceRequest
                {
                    ServiceCode = serviceCode,
                });
                _logger.LogInformation($"GetServiceInfoDtoAsync response:{result.Success}-{result.Result.ToJson()}");
                if (result.Success)
                {
                    use = result.Result;
                }

                return use;
            }
            catch (Exception e)
            {
                _logger.LogError($"GetServiceInfoDtoAsync error: {e}");
                return null;
            }
        }

        public async Task<ProviderInfoDto> GetProviderInfoDtoAsync(string providerCode)
        {
            try
            {
                _logger.LogInformation($"GetProviderInfoDtoAsync request:{providerCode}");
                ProviderInfoDto use = null;

                using var client = new JsonServiceClient(_apiUrl);
                ResponseMessageApi<ProviderInfoDto> result = await client.GetAsync<ResponseMessageApi<ProviderInfoDto>>(new GetProviderRequest
                {
                    ProviderCode = providerCode,
                });
                _logger.LogInformation($"GetProviderInfoDtoAsync response:{result.Success}");
                if (result.Success)
                {
                    use = result.Result;
                }

                return use;
            }
            catch (Exception e)
            {
                _logger.LogError($"GetProviderInfoDtoAsync error: {e}");
                return null;
            }
        }

        public async Task<ProviderInfoDto> GetVenderInfoDtoAsync(string venderCode)
        {
            try
            {
                _logger.LogInformation($"GetVenderInfoDtoAsync request:{venderCode}");
                ProviderInfoDto use = null;

                using var client = new JsonServiceClient(_apiUrl);
                var result = await client.GetAsync<ResponseMessageApi<ProviderInfoDto>>(new GetVenderRequest
                {
                    Code = venderCode,
                });
                _logger.LogInformation($"GetVenderInfoDtoAsync response:{result.Success}-{result.Result.ToJson()}");
                if (result.Success)
                {
                    use = result.Result;
                }

                return use;
            }
            catch (Exception e)
            {
                _logger.LogError($"GetVenderInfoDtoAsync error: {e}");
                return null;
            }
        }

        public async Task<UserLimitDebtDto> GetLimitDebtAccount(string accountCode)
        {
            try
            {
                _logger.LogInformation($"GetLimitDebtAccount request:{accountCode}");
                UserLimitDebtDto use = null;

                using var client = new JsonServiceClient(_apiUrl);
                var result = await client.GetAsync<ResponseMessageApi<UserLimitDebtDto>>(new GetLimitDebtAccountRequest
                {
                    AccountCode = accountCode,
                });
                _logger.LogInformation($"GetLimitDebtAccount response:{result.Success}-{result.Result.ToJson()}");
                if (result.Success)
                {
                    use = result.Result;
                }

                return use;
            }
            catch (Exception e)
            {
                _logger.LogError($"GetLimitDebtAccount error: {e}");
                return null;
            }
        }

        public async Task<UserInfoSaleDto> GetSaleAssignInfo(int userId)
        {
            try
            {
                _logger.LogInformation($"GetSaleAssignInfo request:{userId}");
                UserInfoSaleDto use = null;

                using var client = new JsonServiceClient(_apiUrl);
                var result = await client.GetAsync<ResponseMessageApi<UserInfoSaleDto>>(new GetSaleAssignInfoRequest
                {
                    UserId = userId,
                });
                _logger.LogInformation($"GetSaleAssignInfo response:{result.Success}-{result.Result.ToJson()}");
                if (result.Success)
                {
                    use = result.Result;
                }

                return use;
            }
            catch (Exception e)
            {
                _logger.LogError($"GetSaleAssignInfo error: {e}");
                return null;
            }
        }

        public async Task<List<string>> GetListAccountCode(string accountCode, string currencyCode)
        {
            _logger.LogInformation($"GetAccountRequest request)");
            var lstAccount = new List<string>();
            using var client = new JsonServiceClient(_apiUrlBalance);
            try
            {
                var rs = await client.GetAsync<List<string>>(
                    new BalanceAccountCodesRequest
                    {
                        AccountCode = accountCode,
                        CurrencyCode = currencyCode
                    });
                _logger.LogInformation($"GetAccountRequest return: {rs.ToJson()}");

                lstAccount = rs.ConvertTo<List<string>>();
            }
            catch (Exception ex)
            {
                _logger.LogError($"GetAccountRequest Exception: {ex}");
            }


            return lstAccount;
        }

        public async Task<decimal> GetBalanceTopupDtoAsync(string providerCode)
        {
            try
            {
                _logger.LogInformation($"GetBalanceTopupDtoAsync request:{providerCode}");
                using var client = new JsonServiceClient(_apiUrlTopup);
                var result = await client.GetAsync<NewMessageResponseBase<string>>(new GetBalanceTopupRequest
                {
                    ProviderCode = providerCode,
                });
                _logger.LogInformation($"{providerCode} GetBalanceTopupDtoAsync response:{result.Results}");

                if (result.ResponseStatus.ErrorCode == "01")
                    return Convert.ToDecimal(result.Results);

                return 0;
            }
            catch (Exception e)
            {
                _logger.LogError($"GetBalanceTopupDtoAsync error: {e}");
                return 0;
            }
        }       
    }   
}
