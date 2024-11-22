using System;
using System.Threading.Tasks;
using HLS.Paygate.Gw.Model.Commands;
using HLS.Paygate.Gw.Model.Events;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.AbpConnector;
using HLS.Paygate.Worker.Components.Connectors;
using MassTransit;
using MassTransit.Courier;
using MassTransit.Definition;
using Org.BouncyCastle.Asn1;

namespace HLS.Paygate.Worker.Components.CourierActivities
{
    public class PaymentActivity : IActivity<PaymentArguments, PaymentLog>
    {
        readonly IRequestClient<PaymentProcessCommand> _requestClient;
        private readonly ExternalServiceConnector _externalServiceConnector;
        public PaymentActivity(IRequestClient<PaymentProcessCommand> requestClient, ExternalServiceConnector externalServiceConnector)
        {
            _requestClient = requestClient;
            _externalServiceConnector = externalServiceConnector;
        }

        public async Task<ExecutionResult> Execute(ExecuteContext<PaymentArguments> context)
        {
            var amount = context.Arguments.Amount;
            if (amount <= 0)
                throw new ArgumentNullException(nameof(amount));

            var accountCode = context.Arguments.AccountCode;
            if (string.IsNullOrEmpty(accountCode))
                throw new ArgumentNullException(nameof(accountCode));

            var transCode = context.Arguments.TransCode;
            if (string.IsNullOrEmpty(accountCode))
                throw new ArgumentNullException(nameof(transCode));

            var paymentId = NewId.NextGuid();
            var paymentResponse = await _requestClient.GetResponse<MessageResponseBase>(new
            {
                CorrelationId = paymentId,
                AccountCode = accountCode,
                PaymentAmount = amount,
                CurrencyCode = CurrencyCode.VND.ToString("G"),
                TransRef = transCode,
                context.Arguments.ServiceCode,
                context.Arguments.CategoryCode,
                TransNote = $"Thanh toán cho giao dịch: {transCode}"
            });

            if (paymentResponse.Message.ResponseCode == ResponseCodeConst.Success)
            {
                return context.Completed(new
                {
                    PaymentId = paymentId,
                    PaymentTransCode = paymentResponse.Message.ResponseMessage,
                    context.Arguments.TransCode,
                    context.Arguments.AccountCode,
                    PaymentAmount = amount
                });
            }

            throw new ApplicationException("Payment fail: " + paymentResponse.Message.ResponseCode + "|" +
                                           paymentResponse.Message.ResponseMessage);
        }

        public async Task<CompensationResult> Compensate(CompensateContext<PaymentLog> context)
        {
            //Hoàn tiền
            await context.Publish<PaymentCancelCommand>(new
            {
                CorrelationId = context.Log.PaymentId,
                context.Log.TransCode,
                context.Log.PaymentTransCode,//request.PaymentTransCode,
                TransNote = $"Hoàn tiền cho giao dịch thanh toán: {context.Log.TransCode}",
                RevertAmount = context.Log.PaymentAmount,
                context.Log.AccountCode
            });
            return context.Compensated();
        }
    }

    public interface PaymentArguments
    {
        decimal Amount { get; }
        string AccountCode { get; }
        string CategoryCode { get; }
        string TransCode { get; }
        string ServiceCode { get; }
    }


    public interface PaymentLog
    {
        Guid PaymentId { get; }
        DerInteger PaymentAmount { get; }
        public string PaymentTransCode { get; }
        string AccountCode { get; }
        string TransCode { get; }
    }

    public class PaymentActivityDefinition :
        ActivityDefinition<PaymentActivity, PaymentArguments, PaymentLog>
    {
        public PaymentActivityDefinition()
        {
            ConcurrentMessageLimit = 20;
        }
    }
}
