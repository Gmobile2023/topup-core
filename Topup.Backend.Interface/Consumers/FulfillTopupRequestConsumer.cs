using System.Threading.Tasks;
using HLS.Paygate.Backend.Interface.Connectors;
using MassTransit;
using MassTransit.Definition;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using NLog;
using HLS.Paygate.Gw.Domain.Services;
using HLS.Paygate.Gw.Model.Commands;
using HLS.Paygate.Gw.Model.Enums;
using HLS.Paygate.Shared;

namespace HLS.Paygate.Backend.Interface.Consumers
{
    public class FulfillTopupRequestConsumer : IConsumer<TopupFulfillRequestCommand>
    {
        private readonly IConfiguration _configuration;
        private readonly Logger _logger = LogManager.GetLogger("FulfillTopupRequestConsumer");
        readonly IRequestClient<PaymentProcessCommand> _requestClient;
        private readonly ExternalServiceConnector _externalServiceConnector;
        private readonly ISaleService _saleService;

        public FulfillTopupRequestConsumer(IConfiguration configuration,
            IRequestClient<PaymentProcessCommand> requestClient, ExternalServiceConnector externalServiceConnector,
            ISaleService saleService)
        {
            _configuration = configuration;
            _requestClient = requestClient;
            _externalServiceConnector = externalServiceConnector;
            _saleService = saleService;
        }

        public async Task Consume(ConsumeContext<TopupFulfillRequestCommand> context)
        {
            _logger.LogInformation("FulfillTopup request is comming request: " + context.Message.ToJson());
            var saleRequest = context.Message.SaleRequest;
            //var builder = new RoutingSlipBuilder(context.Message.CorrelationId);

            switch (saleRequest.ServiceCode)
            {
                #region SNS_Game

                case ServiceCodes.PAYMENT_SMS_GAME: //Nạp game qua sms
                    if (saleRequest.CategoryCode == CategoryCodeConst.VTE_SMS_GAME_9029)
                    {
                        _logger.LogInformation("Processing SmsGameActivity");
                        // builder.AddActivity("SmsGame",
                        //     new Uri(_configuration["RabbitMq:EndpointExecute:TopupSmsGame"]),
                        //     new {TopupRequest = saleRequest});
                        //
                        await context.Publish<TopupGameRequestCommand>(new
                        {
                            TopupRequest = saleRequest
                        });
                    }

                    break;

                #endregion

                #region TOPUP

                case ServiceCodes.TOPUP: //giao dịch Topup -- chia các loại ở đây để xử lý riêng

                    if (saleRequest.TopupType == TopupType.TopupPartner &&
                        (saleRequest.CategoryCode == CategoryCodeConst.VNA_POSTPAID ||
                         saleRequest.CategoryCode == CategoryCodeConst.VNA_PREPAID))
                    {
                        _logger.LogInformation($"Processing VinaUssdMappingActivity: {saleRequest.ToJson()}");
                        // builder.AddActivity("VinaUssdMapping",
                        //     new Uri(_configuration["RabbitMq:EndpointExecute:VinaMapping"]),
                        //     new {TopupRequest = saleRequest});

                        await context.Publish<MappingUssdVinaphoneCommand>(new
                        {
                            TopupRequest = saleRequest
                        });
                    }

                    break;

                #endregion

                #region TKC

                case ServiceCodes.TKC: //Nạp qua tài khoản chính. Chỗ này mỗi nhà mạng sẽ xử lý riêng
                    if (saleRequest.CategoryCode == CategoryCodeConst.VTE_TKC)
                    {
                        // builder.AddActivity("ViettelTkcActivity",
                        //     new Uri("rabbitmq://192.168.10.47/paygate/viettel-tkc-execute"),
                        //     new { });
                        // builder.SetVariables(new
                        // {
                        //     TopupRequest = saleRequest
                        // });
                    }

                    if (saleRequest.CategoryCode == CategoryCodeConst.VNA_TKC)
                    {
                        // builder.AddActivity("VinaTkcActivity",
                        //     new Uri("rabbitmq://192.168.10.47/paygate/vina-tkc-execute"),
                        //     new { });
                        // builder.SetVariables(new
                        // {
                        //     TopupRequest = saleRequest
                        // });
                    }

                    if (saleRequest.CategoryCode == CategoryCodeConst.VMS_TKC)
                    {
                        // builder.AddActivity("MobiTkcActivity",
                        //     new Uri("rabbitmq://192.168.10.47/paygate/mobi-tkc-execute"),
                        //     new { });
                        // builder.SetVariables(new
                        // {
                        //     TopupRequest = saleRequest
                        // });
                    }

                    break;

                #endregion

                #region PINCODE Hiện tại k dùng

                // case ServiceCodes.PIN_CODE
                //     : //Giao dịch mua mã thẻ. Tùy khi có nghiệp vụ có thể xử lý chung hoặc riêng theo catecode
                //     if (saleRequest.CategoryCode == CategoryCodeConst.VTE_PINCODE)
                //     {
                //     }
                //     if (saleRequest.CategoryCode == CategoryCodeConst.VNA_PINCODE)
                //     {
                //     }
                //     if (saleRequest.CategoryCode == CategoryCodeConst.VMS_PINCODE)
                //     {
                //     }
                //     break;

                #endregion

                default:

                    break;
            }


            //await context.Execute(builder.Build());

            //Chỗ này xem sửa lại sẽ gọi payment trước. Nếu okie mới thực hiện các bước sau.
            // builder.AddActivity("Payment",
            //     new Uri(_configuration["RabbitMq:EndpointExecute:Payment"]),
            //     new {TopupRequest = saleRequest});
        }
    }

    public class FulfillTopupRequestDefinition :
        ConsumerDefinition<FulfillTopupRequestConsumer>
    {
        public FulfillTopupRequestDefinition()
        {
            ConcurrentMessageLimit = 10;
            EndpointName = "topup-fulfillment";
        }
    }
}
