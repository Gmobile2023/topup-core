using System.Threading.Tasks;
using Orleans;

namespace HLS.Paygate.Balance.Models.Grains;

public interface ITransferGrain : IGrainWithGuidKey
{
    [Transaction(TransactionOption.Create)]
    Task<(decimal, decimal)> Transfer(string fromAccount, string toAccount, decimal amountToTransfer, string transCode);
}