using System;

namespace HLS.Paygate.Gw.Model.Commands.Stock;
// public interface StockCardInventoryCommand : ICommand
// {
//     string StockCode { get; }
//     string ProductCode { get; }
//     decimal CardValue{ get; }
// }

// public interface StockCardExchangeCommand : ICommand
// {
//     string SrcStockCode { get; }
//     string DesStockCode { get; }
//     string ProductCode { get; }
//     // int CardValue { get; }
//     int Amount { get; }
//     string BatchCode { get; }
//     string Description { get; }
// }
// public class StockCardExchangeRequest : StockCardExchangeCommand
// {
//     public string SrcStockCode { get; set; }
//     public string DesStockCode { get; set; }
//     public  string ProductCode { get; set; }
//     // int CardValue { get; }
//     public int Amount  { get; set; }
//     public string BatchCode  { get; set; }
//     public string Description  { get; set; }
//     public Guid CorrelationId { get; set; }
// }

// public interface StockCardSaleCommand : ICommand
// {
//     string StockCode { get; }
//     string ProductCode { get; }
//     // int CardValue { get; }
//     int Amount { get; }
//     string BatchCode { get; }
//     string Description { get; }
// }

// public interface StockCardImportCommand : ICommand
// {
//     string StockCode { get; }
//     string ProductCode { get; }
//     int CardValue { get; }
//     int Amount { get; }
//     string BatchCode { get; }
//     string Description { get; }
//     MessageData<string> Data { get; }
// }

// public interface StockAllocateCommand : ICommand
// {
//     string StockCode { get; }
//     string ProductCode { get; }
//     int Amount { get; }
//     Guid AllocationId { get; }
//     string TransCode { get; }
// }

public interface StockUnAllocateCommand : ICommand
{
    Guid AllocationId { get; }
    string StockCode { get; }
    string ProductCode { get; }
    int Amount { get; }
}

// public interface StockCardsImportCommand : ICommand
// {
//     string BatchCode { get; }
//     string ProductCode { get; }
//     List<CardItemsImport> CardItems { get; }
// }