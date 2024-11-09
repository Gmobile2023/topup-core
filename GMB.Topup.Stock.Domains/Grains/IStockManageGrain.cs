﻿using System;
using System.Threading.Tasks;
using Orleans;

namespace GMB.Topup.Stock.Domains.Grains;

public interface IStockManageGrain : IGrainWithGuidKey
{
    Task Exchange(string srcStockCode, string desStockCode, string productCode, int amount, Guid correlationId,
        string batchCode);
}