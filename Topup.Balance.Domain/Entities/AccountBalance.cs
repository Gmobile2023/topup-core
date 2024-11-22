using System;
using Topup.Balance.Models.Enums;
using Topup.Shared.Utils;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDbGenericRepository.Models;

namespace Topup.Balance.Domain.Entities;

public class AccountBalance : Document
{
    public AccountBalance()
    {
        Id = Guid.NewGuid();
        AddedAtUtc = DateTime.Now;
        Balance = 0;
        LimitOverDraft = 0;
        BlockedMoney = 0;
        MinBalance = 0;
        Status = BalanceStatus.Active;
        LastTransCode = "Init";
    }

    [BsonRepresentation(BsonType.Decimal128, AllowTruncation = true)]
    public decimal? AvailableBalance => Balance + LimitOverDraft - MinBalance - BlockedMoney;

    [BsonRepresentation(BsonType.Decimal128, AllowTruncation = true)]
    public decimal LimitOverDraft { get; set; }

    [BsonRepresentation(BsonType.Decimal128, AllowTruncation = true)]
    public decimal MinBalance { get; set; }

    [BsonRepresentation(BsonType.Decimal128, AllowTruncation = true)]
    public decimal BlockedMoney { get; set; }

    public string AccountCode { get; set; }
    public string CurrencyCode { get; set; }

    [BsonRepresentation(BsonType.Decimal128, AllowTruncation = true)]
    public decimal Balance { get; set; }

    public string LastTransCode { get; set; }
    [BsonRepresentation(BsonType.Int32)] public BalanceStatus Status { get; set; }
    public string CheckSum { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public string AccountType { get; set; }

    public string ToCheckSum()
    {
        var plantText =
            $"{AccountCode}{CurrencyCode}{Balance:0.0000}{LastTransCode}5727407657";
        return Cryptography.HashSHA256(plantText);
    }

    public bool IsValid()
    {
        try
        {
            //return true;
            return ToCheckSum().ToLower() == CheckSum.ToLower();
        }
        catch (Exception)
        {
            return false;
        }
    }

    [BsonRepresentation(BsonType.Int32, AllowTruncation = true)]
    public int ShardCounter { get; set; }
}