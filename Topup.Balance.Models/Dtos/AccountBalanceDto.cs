using System;
using System.Collections.Generic;
using Topup.Balance.Models.Dtos;
using Topup.Shared.Utils;
using Orleans;
using Orleans.CodeGeneration;
using Topup.Balance.Models.Enums;

//[assembly: GenerateSerializer(typeof(AccountBalanceDto))]

namespace Topup.Balance.Models.Dtos;

// [Serializable]
[GenerateSerializer]
public class AccountBalanceDto
{
    public AccountBalanceDto()
    {
        Id = Guid.NewGuid();
        AddedAtUtc = DateTime.Now;
        Balance = 0;
        LimitOverDraft = 0;
        BlockedMoney = 0;
        MinBalance = 0;
        Status = BalanceStatus.Active;
        LastTransCode = "Init";
        RecentTrans = new HashSet<RecentTrans>();
        ShardQueue = new Queue<string>();
    }

    [Id(0)] public Guid Id { get; set; }
    public decimal AvailableBalance
    {
        get => Balance + LimitOverDraft - MinBalance - BlockedMoney;
        set {}
    }
    [Id(2)] public decimal LimitOverDraft { get; set; }
    [Id(3)] public decimal MinBalance { get; set; }
    [Id(4)] public decimal BlockedMoney { get; set; }
    [Id(5)] public string AccountCode { get; set; }
    [Id(6)] public string CurrencyCode { get; set; }
    [Id(7)] public decimal Balance { get; set; }
    [Id(8)] public string LastTransCode { get; set; }
    [Id(9)] public BalanceStatus Status { get; set; }
    [Id(10)] public DateTime? ModifiedDate { get; set; }
    [Id(11)] public DateTime AddedAtUtc { get; set; }
    [Id(12)] public string AccountType { get; set; }
    [Id(13)] public string CheckSum { get; set; }
    [Id(14)]
    public HashSet<RecentTrans> RecentTrans { get; set; }
    [Id(15)]
    public Queue<string> ShardQueue { get; set; }

    [Id(16)] public ushort ShardCounter { get; set; } = 0;
    [Id(17)] public ushort CurrentShardCounter { get; set; } = 0;

    public string ToCheckSum()
    {
        //chỗ này check sum thêm BlockedMoney,LimitOverDraft,MinBalance
        var plantText =
            $"{AccountCode}{CurrencyCode}{Balance:0.0000}{LastTransCode}5727407657";
        return Cryptography.HashSHA256(plantText);
    }
}

[GenerateSerializer]
public class RecentTrans
{
    [Id(0)]
    public DateTime CreatedDate { get; set; }
    [Id(1)]
    public decimal Amount { get; set; }
    [Id(2)]
    public string TransCode { get; set; }
}