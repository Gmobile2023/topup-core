using System;
using System.Threading.Tasks;
using HLS.Paygate.Gw.Model;
using HLS.Paygate.Gw.Model.Commands;
using MassTransit;
using MongoDB.Bson;
using NLog;
using ServiceStack;

namespace HLS.Paygate.Stock.Components.Consumers
{
    public class CardCallBackConsumer : IConsumer<CardCallBackCommand>
    {
        private readonly Logger _logger = LogManager.GetLogger("CardCallBackConsumer");

        public async Task Consume(ConsumeContext<CardCallBackCommand> context)
        {
            try
            {
                _logger.LogInformation($"CardCallBackConsumer request:{BsonExtensionMethods.ToJson(context.Message)}");
                var mess = context.Message;

                var client = new JsonServiceClient("callbackUrl")
                {
                };
                var request = new CardCallBackRequest
                {
                    Serial = mess.Serial,
                    CardCode = mess.CardCode,
                    CardValue = mess.CardValue,
                    ResponseMessage = mess.ResponseMessage,
                    RequestValue = mess.RequestValue,
                    ResponseCode = mess.ResponseCode,
                    TransCode = mess.TransCode,
                    RequestCode = mess.TransRef
                };
                var callBackReturn = await client.PostAsync<string>(request);
                _logger.LogInformation($"CardCallBackConsumer return:{callBackReturn}");
            }
            catch (Exception e)
            {
                _logger.LogError("CardCallBackConsumer error:" + e);
            }
        }
    }
}
