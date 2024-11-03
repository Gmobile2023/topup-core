using System;
using System.Linq;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Configuration;
using NLog;
using HLS.Paygate.Gw.Domain.Services;
using HLS.Paygate.Gw.Model.Dtos;
using HLS.Paygate.Gw.Model.Enums;
using HLS.Paygate.Gw.Model.Events;
using HLS.Paygate.Shared.Utils;

namespace HLS.Paygate.Backend.Interface.Consumers
{
    public class TopupItemConsumer : IConsumer<TopupItemCommand>
    {
        private readonly IConfiguration _configuration;
        private readonly Logger _logger = LogManager.GetLogger("TopupItemConsumer");
        private readonly ISaleService _saleService;

        public TopupItemConsumer(ISaleService saleService, IConfiguration configuration)
        {
            _saleService = saleService;
            _configuration = configuration;
        }

        public async Task Consume(ConsumeContext<TopupItemCommand> context)
        {
            _logger.LogInformation("TopupItemCommand comming request: " + context.Message.TransCode + " " +
                         context.Message.TopupItemType);
            var saleRequest = await _saleService.SaleRequestGetAsync(context.Message.TransCode);
            var lstTopupItem = context.Message.CardItems.Select(item => new SaleItemDto
                {
                    Amount = item.CardValue,
                    Serial = item.Serial,
                    CardExpiredDate = item.ExpireDate,
                    Status = TopupStatus.Success,
                    Vendor = item.Vendor,
                    CardCode = item.CardCode.EncryptTripDes(),
                    CardValue = item.CardValue,
                    ServiceCode = saleRequest.ServiceCode,
                    PartnerCode = saleRequest.PartnerCode,
                    SaleType = context.Message.TopupItemType,
                    SaleTransCode = saleRequest.TransCode,
                    CreatedTime = DateTime.Now
                })
                .ToList();
            await _saleService.SaleItemListCreateAsync(lstTopupItem);
        }
    }
}
