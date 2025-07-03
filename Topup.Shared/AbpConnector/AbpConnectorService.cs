using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DnsClient.Protocol;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nest;
using ServiceStack;
using ServiceStack.Caching;
using Topup.Shared.CacheManager;

namespace Topup.Shared.AbpConnector;

public class ExternalServiceConnector
{
    private readonly string _apiUrl;
    private readonly ILogger<ExternalServiceConnector> _logger;
    private readonly ICacheManager _cacheManager;
    private static int _indexRoundRobinTrans;

    public ExternalServiceConnector(IConfiguration configuration, ILogger<ExternalServiceConnector> logger,
        ICacheManager cacheManager)
    {
        _logger = logger;
        _apiUrl = configuration["ServiceUrlConfig:GatewayPrivate"];
        _cacheManager = cacheManager;
    }

    /// <summary>
    ///     Hàm này sẽ check productcode ra mệnh giá và chiết khấu luôn
    /// </summary>
    /// <param name="account"></param>
    /// <param name="productCode"></param>
    /// <param name="receiverInfo"></param>
    /// <param name="amount"></param>
    /// <param name="quantity"></param>
    /// <returns></returns>
    public async Task<ProductDiscountDto> CheckProductDiscount(string transCode, string account, string productCode,
        decimal amount = 0,
        int quantity = 1)
    {
        try
        {
            _logger.LogInformation($"CheckProductDiscount request:{transCode}- {account}-{productCode}");

            // var productDiscountinfo =
            //     await _cacheManager.GetEntity<ProductDiscountDto>(
            //         $"{ProductDiscountDto.CacheKey}:{account}:{productCode}");
            // if (productDiscountinfo != null)
            //     _logger.LogInformation(
            //         $"CheckProductDiscount request:{transCode}- {account}-{productCode} from Cache => {productDiscountinfo.ToJson()}");

            // else
            // {
            using var client = new JsonServiceClient(_apiUrl);
            var result = await client.GetAsync<ResponseMessageApi<ProductDiscountDto>>(
                new GetProductDiscountRequest
                {
                    AccountCode = account,
                    ProductCode = productCode,
                    TransCode = transCode
                });
            //_logger.LogInformation($"CheckProductDiscount response:{result.Success}-{result.Result?.ToJson()}");
            if (!result.Success) return null;
            var rs = result.Result;
            if (rs == null)
                return null;
            if (string.IsNullOrEmpty(rs.ProductCode))
            {
                _logger.LogInformation($"{transCode}-Can not get product:{productCode}");
                return null;
            }

            if (rs.ProductValue <= 0)
            {
                _logger.LogInformation($" {transCode} Can not get productvalue:{productCode}");
                return null;
            }

            var productDiscountinfo = result.Result;
            // var expTime = (productDiscountinfo.ToDate - DateTime.Now).TotalSeconds;
            // if (expTime > 0)
            // {
            //     await _cacheManager.AddEntity($"{ProductDiscountDto.CacheKey}:{account}:{productCode}",
            //         productDiscountinfo, TimeSpan.FromSeconds(expTime));
            // }
            _logger.LogInformation(
                $"CheckProductDiscount request:{transCode}- {account}-{productCode} from API => {productDiscountinfo.ToJson()}");
            var policy = new ProductDiscountDto();
            var price = amount > 0 ? amount : productDiscountinfo.ProductValue;
            policy.PaymentAmount = price * quantity;
            if (productDiscountinfo.FixAmount != null)
                productDiscountinfo.FixAmount *= quantity;
            policy.DiscountAmount = 0;
            if (productDiscountinfo.FixAmount != null && productDiscountinfo.DiscountValue != null)
            {
                var discountPercent = productDiscountinfo.DiscountValue * policy.PaymentAmount / 100;
                if (discountPercent < productDiscountinfo.FixAmount)
                    policy.DiscountAmount = discountPercent ?? 0;
                else policy.DiscountAmount = productDiscountinfo.FixAmount ?? 0;
            }
            else if (productDiscountinfo.DiscountValue != null)
            {
                policy.DiscountAmount = (productDiscountinfo.DiscountValue ?? 0) * policy.PaymentAmount / 100;
            }
            else if (productDiscountinfo.FixAmount != null)
            {
                policy.DiscountAmount = productDiscountinfo.FixAmount ?? 0;
            }

            policy.DiscountAmount = Math.Round(policy.DiscountAmount);
            policy.PaymentAmount -= policy.DiscountAmount;
            policy.ProductValue = productDiscountinfo.ProductValue;
            if (policy.ProductValue <= 0 || policy.PaymentAmount <= 0)
            {
                SendTeleError("Giao dịch không thành công.Không tìm thấy chính sách chiết khấu hoặc sản phẩm",
                    $"{transCode} -  {account} - {productCode}");
            }

            return policy;
        }
        catch (Exception e)
        {
            SendTeleError("Lỗi chính sách chiết khấu",
                $"{transCode} -  {account} - {productCode} - {amount} - {quantity} \n {e.Message}\n {e.StackTrace}");
            _logger.LogError(e, $"{transCode} - CheckProductDiscount error: {e}");
            return null;
        }
    }

    public async Task<ProductFeeDto> GetProductFee(string transCode, string accountcode, string productCode,
        decimal amount)
    {
        try
        {
            _logger.LogInformation($"GetProductFee request:{transCode} - {accountcode}-{productCode}-{amount}");

            using (var client = new JsonServiceClient(_apiUrl))
            {
                var result = await client.GetAsync<ResponseMessageApi<ProductFeeDto>>(
                    new GetProductFeeRequest
                    {
                        AccountCode = accountcode,
                        ProductCode = productCode,
                        Amount = amount
                    });
                // _logger.LogInformation($"GetProductFee response:{result.Success}-{result.Result?.ToJson()}");
                if (result == null || !result.Success) return null;
                var rs = result.Result;
                if (rs == null)
                    return null;
                if (string.IsNullOrEmpty(rs.ProductCode))
                {
                    _logger.LogInformation($"GetProductFee{transCode} Can not get product fee:{productCode}");
                    return null;
                }

                if (rs.Amount <= 0)
                {
                    _logger.LogInformation($"GetProductFee {transCode} Amount invalid:{productCode}");
                    return null;
                }

                return rs;
            }
        }
        catch (Exception e)
        {
            SendTeleError("Lỗi lấy chính sách phí",
                $"{transCode} -  {accountcode} - {productCode} - {amount} \n {e.Message}\n {e.StackTrace}");
            _logger.LogError(e, $" {transCode} GetProductFee error: {e}");
            return null;
        }
    }


    public async Task<ProductInfoDto> GetProductInfo(string transCode, string categoryCode, string productCode,
        decimal amount)
    {
        try
        {
            _logger.LogInformation($"GetProductInfo: {transCode}- {productCode}-{amount} - {categoryCode}");
            var cacheInfo =
                await _cacheManager.GetEntity<ProductInfoDto>(
                    $"{ProductInfoDto.CacheKey}:{categoryCode}:{productCode}");
            if (cacheInfo != null)
            {
                _logger.LogInformation($"{transCode}-Get product info from cache : {cacheInfo.ToJson()}");
                return cacheInfo;
            }

            _logger.LogInformation($"GetProductInfo: {transCode}- Call api: ");
            using (var client = new JsonServiceClient(_apiUrl))
            {
                var result = await client.GetAsync<ResponseMessageApi<ProductInfoDto>>(
                    new GetProductRequest
                    {
                        CategoryCode = categoryCode,
                        ProductCode = productCode,
                        Amount = amount
                    });
                // _logger.LogInformation($"GetProductFee response:{result.Success}-{result.Result?.ToJson()}");
                if (!result.Success) return null;
                var rs = result.Result;
                if (rs == null)
                    return null;
                await _cacheManager.AddEntity($"{ProductInfoDto.CacheKey}:{categoryCode}:{productCode}", rs);
                _logger.LogInformation($"{transCode}-Get product info from api : {rs.ToJson()}");
                return rs;
            }
        }
        catch (Exception e)
        {
            SendTeleError("Lôi lấy thông tin sản phẩm",
                $"{transCode} -  {categoryCode} - {productCode} - {amount} \n {e.Message}\n {e.StackTrace}");
            _logger.LogError(e, $"{transCode} - GetProductInfo error: {e}");
            return null;
        }
    }


    public async Task<ResponseMessageApi<LimitProductDetailDto>> CheckLimitProductPerDay(string transCode,
        string accountcode,
        string productCode,
        decimal totalamount, int totlaQuantity)
    {
        try
        {
            _logger.LogInformation(
                $"CheckLimitProductPerDay request: {transCode} - {accountcode}-{productCode}-{totalamount}-{totlaQuantity}");
            using var client = new JsonServiceClient(_apiUrl);
            var result = await client.GetAsync<ResponseMessageApi<LimitProductDetailDto>>(
                new GetLimitProductPerDayRequest
                {
                    AccountCode = accountcode,
                    ProductCode = productCode,
                    TotalAmount = totalamount,
                    TotalQuantity = totlaQuantity
                });
            _logger.LogInformation(
                $"CheckLimitProductPerDay response: {transCode}  {result.Success}-{result.Result.ToJson()}");
            return result;
        }
        catch (Exception e)
        {
            await SendTeleError("Lỗi Check hạn mức sản phẩm",
                $"{transCode} -  {accountcode} - {totalamount} - {productCode}-{totlaQuantity} \n {e.Message}\n {e.StackTrace}");
            _logger.LogError(e, $"{transCode}-CheckLimitProductPerDay error: {e}");
            return new ResponseMessageApi<LimitProductDetailDto>
            {
                Success = false,
                Error = new ErrorMessage
                {
                    Message = "Check hạn mức sản phẩm không thành công"
                }
            };
        }
    }

    void SetConfigsByGroup(List<ServiceConfiguration> n, ref List<ServiceConfiguration> tempConfigs)
    {
        if (n.Count > 0)
        {
            var n1 = n.GroupBy(p => p.WorkShortCode).Select(p => new
            {
                p.Key,
                Count = p.Count()
            }).ToList();

            foreach (var item in n1)
            {
                if (item.Count == 1) continue; // Group có 1 thằng thì không quan tâm nữa

                var n2 = n.Where(p => p.WorkShortCode == item.Key).ToList();

                var total = n2.Sum(p => p.RateRunning); //Tổng tỉ lệ

                var totalTransCount = 0L;

                foreach (var item2 in n2)
                {
                    item2.RateRunning = item2.RateRunning / total * 100;
                    item2.TransCount = _cacheManager.GetEntity<long>(
                        $"PayGate_RatingTrans:Items:{item2.ProviderCode}:{item2.AccountCode}:{item2.ServiceCode}:{item2.CategoryCode}:{item2.ProductCode}_{true}").Result;
                    totalTransCount += item2.TransCount;
                }

                var n3 = n2.MinBy(p =>
                    Convert.ToDouble(p.TransCount) / Convert.ToDouble(totalTransCount) * 100 /
                    Convert.ToDouble(p.RateRunning)); //Lấy thằng có độ chênh tỉ lệ cao nhất

                var selected = false;
                var t = new List<ServiceConfiguration>();

                foreach (var serviceConfiguration in
                         tempConfigs) //Thay thế thằng có tỉ lệ cao nhất và xóa các thằng còn lại
                {
                    if (serviceConfiguration.WorkShortCode == n3.WorkShortCode && serviceConfiguration.RateRunning > 0)
                    {
                        if (selected) continue;
                        n3.Priority = serviceConfiguration.Priority; //Gán lại độ ưu tiên
                        t.Add(n3);
                        selected = true;
                    }
                    else
                    {
                        t.Add(serviceConfiguration);
                    }
                }

                tempConfigs = t;
            }
        }
    }

    public async Task<List<ServiceConfiguration>> ServiceConfigurationAsync(string transCode, string accountCode,
        string serviceCode,
        string categoryCode,
        string productCode = "", bool isCheckChannel = false)
    {
        try
        {
            _logger.LogInformation(
                $"ServiceConfiguration request: {transCode} - {serviceCode}-{categoryCode}-{productCode}-{isCheckChannel}");

            var configs = await _cacheManager.GetEntity<List<ServiceConfiguration>>(
                $"{ServiceConfiguration.CacheKey}:{accountCode}:{serviceCode}:{categoryCode}:{productCode}_{isCheckChannel}");
            if (configs is { Count: > 0 })
            {
                _logger.LogInformation($"ServiceConfiguration : {transCode} - ==> get from cache : {configs.Count}");
            }
            else
            {
                //Nếu cấu hình lại kênh thì reset roundrobin
                //_roundRobinList?.Reset();
                using var client = new JsonServiceClient(_apiUrl);
                //using var client = new JsonServiceClient("https://localhost:44301/");
                var result = await client.GetAsync<ResponseMessageApi<List<ServiceConfiguration>>>(
                    new GetConfigurationRequest
                    {
                        AccountCode = accountCode,
                        CategoryCode = categoryCode,
                        ServiceCode = serviceCode,
                        ProductCode = productCode,
                        IsCheckChannel = isCheckChannel
                    });
                _logger.LogInformation(
                    $"ServiceConfiguration response:{transCode}-{result.Success}-{result.Result.ToJson()}");
                if (result.Success)
                {
                    configs = result.Result;
                    _logger.LogInformation(
                        $"ServiceConfiguration response : {transCode} -{accountCode} - {serviceCode}-{categoryCode}-{productCode}-{isCheckChannel} from api => {configs.ToJson()}");
                    if (configs is { Count: > 0 })
                    {
                        await _cacheManager.AddEntity(
                            $"{ServiceConfiguration.CacheKey}:{accountCode}:{serviceCode}:{categoryCode}:{productCode}_{isCheckChannel}",
                            configs);
                    }

                    if (configs is not { Count: > 0 })
                    {
                        _logger.LogWarning(
                            $"Giao dịch không thành công. Không có cấu hình dịch vụ nào sẵn sàng {transCode} -  {serviceCode} -{categoryCode} {productCode}");
                        await SendTeleError("Giao dịch không thành công. Không có cấu hình dịch vụ nào sẵn sàng",
                            $"{transCode} -  {serviceCode} -{categoryCode} {productCode}");
                    }
                }
                else
                {
                    _logger.LogError(
                        $"Giao dịch không thành công. Không lấy được cấu hình dịch vụ {transCode} -  {serviceCode} -{categoryCode} {productCode}");
                    await SendTeleError("Giao dịch không thành công. Không lấy được cấu hình dịch vụ",
                        $"{transCode} -  {serviceCode} -{categoryCode} {productCode}");
                }
            }

            if (configs == null || !configs.Any()) return configs;

            //TODO: Lấy tỉ lệ chạy của các nhà cung cấp Namnl (2023/11/11)
            _logger.LogInformation($"ServiceConfiguration {transCode} - before_configs : {configs.Select(x => x.ProviderCode).ToJson()}");
            var n = configs.FindAll(p => !string.IsNullOrEmpty(p.WorkShortCode) && p.RateRunning > 0 && !string.IsNullOrEmpty(p.AccountCode));

            var tempConfigs = configs;

            SetConfigsByGroup(n, ref tempConfigs);

            //Xét đến trường hợp chung
            n = tempConfigs.FindAll(p => !string.IsNullOrEmpty(p.WorkShortCode) && p.RateRunning > 0 && string.IsNullOrEmpty(p.AccountCode));

            SetConfigsByGroup(n, ref tempConfigs);

            configs = tempConfigs;

            foreach (var item in configs.Where(item =>
                         item.IsRoundRobinAccount && item.SubConfiguration != null && item.SubConfiguration.Any()))
            {
                _logger.LogInformation(
                    $"ServiceConfiguration process get round robin account : {transCode}-{item.ProviderCode}-total sub:{item.SubConfiguration.Count}");

                var newItem = await GetRoundRobinConfiguration(item);
                _logger.LogInformation(
                    $"ServiceConfiguration process get round robin account return: {transCode}-{newItem?.ProviderCode}");
                if (newItem == null || newItem.ProviderCode == item.ProviderCode) continue;
                //Gán lại provider sau khi roundrobin
                item.ProviderCode = newItem.ProviderCode;
                item.ProviderName = newItem.ProviderName;
                item.ParentProvider = newItem.ParentProvider;
                item.TimeOut = newItem.TimeOut;
                item.ProviderMaxWaitingTimeout = newItem.ProviderMaxWaitingTimeout;
                item.ProviderSetTransactionTimeout = newItem.ProviderSetTransactionTimeout;
                item.IsEnableResponseWhenJustReceived = newItem.IsEnableResponseWhenJustReceived;
                item.StatusResponseWhenJustReceived = newItem.StatusResponseWhenJustReceived;
                item.WaitingTimeResponseWhenJustReceived = newItem.WaitingTimeResponseWhenJustReceived;
                item.Priority = newItem.Priority;
                item.RateRunning = newItem.RateRunning;
                item.TimeAwaitCheckTrans = newItem.TimeAwaitCheckTrans;
                item.Retry = newItem.Retry;
                item.TransCodeConfig = newItem.TransCodeConfig;
            }

            _logger.LogInformation($"ServiceConfiguration {transCode} - after_set_configs : {configs.Select(x => x.ProviderCode).ToJson()}");
            return configs;
        }
        catch (Exception e)
        {
            await SendTeleError("Lỗi lấy cấu hình dịch vụ",
                $"{transCode} -  {serviceCode} - {categoryCode} - {productCode}-{isCheckChannel} \n {e.Message}\n {e.StackTrace}");
            _logger.LogError(e, $"{transCode} - ServiceConfigurationAsync error: {e}");
            return null;
        }
    }

    private async Task SendTeleError(string title, string msg)
    {
        try
        {
            Task.Factory.StartNew(() =>
            {
                _logger.LogInformation($"Send tele msg: {title} - {msg}");
                using var client = new JsonServiceClient(_apiUrl);
                client.PostAsync<object>(
                    new SendTeleMsg
                    {
                        Title = title,
                        BotType = "Dev",
                        Module = "WORKER",
                        Message = msg,
                        MessageType = "Error"
                    });
  
            });
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Send tele msg  {title} - {msg} ex : {e.Message} ");
        }
    }

    //todo là round robin trên từng server
    private Task<ServiceConfiguration> GetRoundRobinConfiguration(ServiceConfiguration parentProvider)
    {
        try
        {
            var lst = parentProvider.SubConfiguration;
            lst.Insert(0, parentProvider); //cho thằng cha vào roundrobin luôn
            if (!lst.Any()) return Task.FromResult<ServiceConfiguration>(null);
            var account = lst[_indexRoundRobinTrans];
            _indexRoundRobinTrans = (_indexRoundRobinTrans + 1) % lst.Count;
            return Task.FromResult(account);
        }
        catch (Exception e)
        {
            _logger.LogError($"GetRoundRobinConfiguration error:{e}");
            return Task.FromResult<ServiceConfiguration>(null);
        }
    }

    private async Task<List<ServiceConfiguration>> GetSubConfiguration(ServiceConfiguration parentProvider)
    {
        try
        {
            var key = $"{"PayGate_ServiceConfiguations_RoundRobin"}:{parentProvider.ProviderCode}";
            var configs =
                await _cacheManager.GetEntity<List<ServiceConfiguration>>(key);
            return configs is { Count: > 0 } ? configs : new List<ServiceConfiguration>();
        }
        catch (Exception e)
        {
            return null;
        }
    }

    private async Task SetRoundRobinConfiguration(ServiceConfiguration parentConfiguration)
    {
        try
        {
            // if (configuration.IsReady)
            //     configuration.SubConfiguration.Insert(0, configuration);
            // if (_roundRobinList?.Next() == null)
            //     _roundRobinList ??= new RoundRobinList<ServiceConfiguation>(configuration.SubConfiguration);
            var key = $"{"PayGate_ServiceConfiguations_RoundRobin"}:{parentConfiguration.ProviderCode}";
            await _cacheManager.AddEntity(key, parentConfiguration.SubConfiguration);
        }
        catch (Exception e)
        {
            _logger.LogError($"SetRoundRobinConfiguration error:{e}");
        }
    }
}

#region Request

[Route("/api/v1/AgentService/GetCommission")]
public class GetCommissionRequest
{
    public string AccountCode { get; set; }
    public string ProductCode { get; set; }
}

[Route("/api/v1/common/tele/send")]
public class SendTeleMsg
{
    public string Title { get; set; }
    public string Message { get; set; }
    public string Module { get; set; }
    public string Code { get; set; }
    public string MessageType { get; set; }
    public string BotType { get; set; }
}

[Route("/api/v1/Products/GetProductInfo")]
public class GetProductRequest
{
    public string CategoryCode { get; set; }
    public string ProductCode { get; set; }
    public decimal Amount { get; set; }
}

// [Route("/api/v1/AgentService/GetDiscount")]
// public class GetDiscountRequest
// {
//     public string AccountCode { get; set; }
//     public string ProductCode { get; set; }
// }

[Route("/api/v1/AgentService/GetProductDiscount")]
public class GetProductDiscountRequest
{
    public string AccountCode { get; set; }
    public string ProductCode { get; set; }
    public string ReceiverInfo { get; set; }
    public string TransCode { get; set; }
}

[Route("/api/v1/AgentService/GetProductFee")]
public class GetProductFeeRequest
{
    public string AccountCode { get; set; }
    public string ProductCode { get; set; }
    public decimal Amount { get; set; }
}

[Route("/api/v1/AgentService/GetLimitProductPerDay")]
public class GetLimitProductPerDayRequest
{
    public string AccountCode { get; set; }
    public string ProductCode { get; set; }
    public decimal TotalAmount { get; set; }
    public int TotalQuantity { get; set; }
}

[Route("/api/v1/ServiceConfigurations/GetServiceConfiguations")]
//[Route("/api/services/app/ServiceConfigurations/GetServiceConfiguations")]
public class GetConfigurationRequest
{
    public string ServiceCode { get; set; }
    public string CategoryCode { get; set; }
    public string ProductCode { get; set; }

    public string AccountCode { get; set; }
    public bool IsCheckChannel { get; set; }
}

public class ServiceConfiguration
{
    public const string CacheKey = "PayGate_ServiceConfiguations";

    public int Priority { get; set; }
    public virtual string ProviderCode { get; set; }
    public virtual string ProviderName { get; set; }
    public virtual string ExtraInfo { get; set; }
    public virtual string Name { get; set; }

    /// <summary>
    ///     Địa chỉ url api
    /// </summary>
    public virtual string BaseUrl { get; set; }

    /// <summary>
    ///     Passwork kết nối API
    /// </summary>
    public virtual string ApiPass { get; set; }

    /// <summary>
    ///     Tài khoản kết nối API
    /// </summary>
    public virtual string ApiAccount { get; set; }

    /// <summary>
    ///     API Key
    /// </summary>
    public virtual string ApiKey { get; set; }

    /// <summary>
    ///     Cấu hình timeout gọi đối tác
    /// </summary>
    public virtual int? TimeOut { get; set; }

    /// <summary>
    ///     Số lần retry
    /// </summary>
    public virtual byte? Retry { get; set; }

    /// <summary>
    ///     Thời gian sleep giữa các lần retry
    /// </summary>
    public virtual int? SleepRetry { get; set; }

    /// <summary>
    ///     Thời gian gọi lại check giao dịch
    /// </summary>

    public virtual int? TimeAwaitCheckTrans { get; set; }

    public virtual int? MaxConnection { get; set; }
    public virtual string Description { get; set; }
    public string ServiceCode { get; set; }
    public string CategoryCode { get; set; }
    public bool IsOpened { get; set; }

    public string AccountCode { get; set; }
    public string ProductCode { get; set; }
    public decimal? ProductValue { get; set; }
    public string TransCodeConfig { get; set; }
    public bool IsSlowTrans { get; set; }

    //nhannv: 
    //Thoi gian xu ly giao dich
    public int? ProviderSetTransactionTimeout { get; set; }

    //Thoi gian check ket qua
    public int? ProviderMaxWaitingTimeout { get; set; }

    /// <summary>
    /// Trả kết quả ngay khi tiếp nhận thành công giao dịch
    /// </summary>
    public bool IsEnableResponseWhenJustReceived { get; set; }

    /// <summary>
    /// Trạng thái trả ra tiếp nhận giao dịch
    /// </summary>
    public string StatusResponseWhenJustReceived { get; set; }

    public int WaitingTimeResponseWhenJustReceived { get; set; }
    public bool IsRoundRobinAccount { get; set; }
    public bool IsReady { get; set; }
    public string ParentProvider { get; set; }
    public List<ServiceConfiguration> SubConfiguration { get; set; }
    public string AllowTopupReceiverType { get; set; }
    public decimal RateRunning { get; set; }
    public string WorkShortCode { get; set; }
    public long TransCount { get; set; }
}

public class ProductDiscountDto
{
    public const string CacheKey = "PayGate_ProductDiscount";
    public decimal ProductValue { get; set; }
    public string ProductCode { get; set; }
    public string ProductName { get; set; }
    public decimal? DiscountValue { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal PaymentAmount { get; set; }
    public DateTime ToDate { get; set; }

    public decimal? FixAmount { get; set; }
    // public string CategoryCode { get; set; }
    // public string CategoryName { get; set; }
}

public class ProductFeeDto
{
    public const string CacheKey = "PayGate_ProductFeeInfo";
    public string ProductCode { get; set; }
    public string ProductName { get; set; }
    public decimal Amount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal FeeValue { get; set; }
    public int FeeId { get; set; }
    public int FeeDetailId { get; set; }
    public decimal? AmountMinFee { get; set; }
    public decimal? SubFee { get; set; }
    public decimal? AmountIncrease { get; set; }
    public decimal? MinFee { get; set; }
    public DateTime ToDate { get; set; }
}

public class LimitProductDetailDto
{
    public int? LimitQuantity { get; set; }

    public decimal? LimitAmount { get; set; }

    public int? ProductId { get; set; }

    public string ProductName { get; set; }

    public string ProductType { get; set; }

    public string ServiceName { get; set; }
}

public class ProductInfoDto
{
    public const string CacheKey = "PayGate_ProductInfo";
    public string ProductCode { get; set; }
    public decimal ProductValue { get; set; }
    public int Status { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
}

#endregion