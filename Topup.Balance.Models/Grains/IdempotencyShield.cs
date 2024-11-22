using System.Collections.Generic;
using System.Linq;
using Orleans;
using Topup.Balance.Models.Dtos;

namespace Topup.Balance.Models.Grains;

[GenerateSerializer]
public class IdempotencyShield(int maxSize)
{
    [Id(0)]
    private readonly LinkedList<RecentTrans> _recentTrans = new();

    public bool CheckIdempotency(string transCode)
    {
        return _recentTrans.Any(trans => trans.TransCode == transCode);
    }

    public void CommitTransaction(RecentTrans transaction)
    {
        if (_recentTrans.Count >= maxSize)
        {
            _recentTrans.RemoveFirst();
        }
        _recentTrans.AddLast(transaction);
    }

    public decimal RollbackTransaction(string transCode)
    {
        var transaction = _recentTrans.FirstOrDefault(trans => trans.TransCode == transCode);
        if (transaction != null)
        {
            var amount = transaction.Amount;
            transaction.Amount = 0;
            return amount;
        }

        return 0;
    }
}