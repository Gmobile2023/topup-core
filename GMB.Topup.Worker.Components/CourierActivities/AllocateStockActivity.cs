using System;
using System.Threading.Tasks;
using HLS.Paygate.Gw.Model.Commands;
using HLS.Paygate.Gw.Model.Commands.Stock;
using HLS.Paygate.Gw.Model.Events.Stock;
using MassTransit;
using MassTransit.Courier;
using MassTransit.Definition;

namespace HLS.Paygate.Worker.Components.CourierActivities
{
    public class AllocateStockActivity : IActivity<AllocateStockArguments, AllocateStockLog>
    {
        readonly IRequestClient<StockAllocateCommand> _client;

        public AllocateStockActivity(IRequestClient<StockAllocateCommand> client)
        {
            _client = client;
        }

        public async Task<ExecutionResult> Execute(ExecuteContext<AllocateStockArguments> context)
        {
            var transCode = context.Arguments.TransCode;   
            var productCode = context.Arguments.ProductCode;
            if (string.IsNullOrEmpty(productCode))
                throw new ArgumentNullException(nameof(productCode));
            var stockCode = context.Arguments.StockCode;
            if (string.IsNullOrEmpty(stockCode))
                throw new ArgumentNullException(nameof(stockCode));
            var quantity = context.Arguments.Quantity;
            if (quantity <= 0)
                throw new ArgumentNullException(nameof(quantity));
            
            var allocationId = NewId.NextGuid();

            var response = await _client.GetResponse<StockAllocated>(new
            {
                AllocationId = allocationId,
                TransCode = transCode,
                Quantity = quantity,
                StockCode = stockCode
            });

            return context.Completed(new {AllocationId = allocationId});
        }

        public async Task<CompensationResult> Compensate(CompensateContext<AllocateStockLog> context)
        {
            await context.Publish<StockUnAllocateCommand>(new
            {
                context.Log.AllocationId,
                Reason = "Order Faulted"
            });

            return context.Compensated();
        }
    }

    public interface AllocateStockArguments
    {
        string ProductCode { get; }
        string StockCode { get; }
        int Quantity { get; }
        string TransCode { get; }
    }

    public class AllocateStockActivityDefinition:
        ActivityDefinition<AllocateStockActivity, AllocateStockArguments, AllocateStockLog>
    {
        public AllocateStockActivityDefinition()
        {
            ConcurrentMessageLimit = 10;
        }
    }

    public interface AllocateStockLog
    {
        Guid AllocationId { get; }
    }
}