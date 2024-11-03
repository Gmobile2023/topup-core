using System;
using System.Collections.Generic;
using ServiceStack;
using HLS.Paygate.Gw.Model.Commands;
using HLS.Paygate.Gw.Model.Commands.Stock;
using HLS.Paygate.Shared;
using HLS.Paygate.Stock.Contracts.Dtos;

namespace HLS.Paygate.Stock.Contracts.ApiRequests
{
    [Route("/api/v1/stock/cards_sale", "POST")]
    public class CardSaleRequest : IPost, StockCardSaleCommand, IReturn<MessageResponseBase>
    {
        // public Guid Id { get; set; }

        [ApiMember(ExcludeInSchema = false, IsRequired = true, Description = "Mã kho")]
        public string StockCode { get; set; }
        // public string DesStockCode { get; set; }
        // public byte Status { get; set; }
        [ApiMember(ExcludeInSchema = false, IsRequired = true, Description = "Số lượng")]
        public int Amount { get; set; }

        public string BatchCode { get; }

        // public string Serial { get; set; }
        // public string CardCode { get; }

        [ApiMember(ExcludeInSchema = false, IsRequired = true, Description = "Mệnh giá")]
        public int CardValue { get; set; }

        [ApiMember(ExcludeInSchema = false, IsRequired = true, Description = "Nhà mạng")]
        public string ProductCode { get; set; }
        //[ApiMember(ExcludeInSchema = true)]
        public Guid CorrelationId => Guid.NewGuid();
        // public string Command => "SALE";
        [ApiMember(ExcludeInSchema = false, Description = "Mô tả")]
        public string Description { get; set; }
        // public string AccountCode { get; set; }
        // public bool IsGetCardInStock { get; }
        // public bool IsCardTimeOut { get; }
    }
}