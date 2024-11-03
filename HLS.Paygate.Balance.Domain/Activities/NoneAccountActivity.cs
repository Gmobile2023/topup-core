using System.Threading.Tasks;
using Orleans.Sagas;

namespace HLS.Paygate.Balance.Domain.Activities;

public class NoneAccountActivity : IActivity
{
    public const string HAS_RESULT = "HasResult";
    public Task Execute(IActivityContext context)
    {
        var hasResult = context.SagaProperties.Get<bool>(HAS_RESULT);
        if (hasResult)
            context.SagaProperties.Add("Result", 0);
        
        return Task.CompletedTask;
    }

    public Task Compensate(IActivityContext context)
    {
        return Task.CompletedTask;
    }

    public Task<bool> HasResult()
    {
        return Task.FromResult(true);
    }
}