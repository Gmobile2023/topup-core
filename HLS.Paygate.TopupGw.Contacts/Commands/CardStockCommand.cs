using System;
using System.Collections.Generic;
using MassTransit;
using TW.Paygate.Stock.Contracts.Dtos;

//using MassTransit;

namespace TW.Paygate.Gw.Model.Commands
{
    public interface CardStockCommand : CorrelatedBy<Guid>
    {
        Guid Id { get; set; }
        string StockCode { get; }
        string Command { get; }
        string DesStockCode { get; }
        string Telco { get; }
        string Serial { get; }
        string CardCode { get; }
        int CardValue { get; } 
        int Amount { get; }
        string Description { get; }
        string AccountCode { get; }
    }
}