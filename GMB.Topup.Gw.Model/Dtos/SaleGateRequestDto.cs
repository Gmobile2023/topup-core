using GMB.Topup.Shared;
using System;

namespace GMB.Topup.Gw.Model.Dtos
{
    public class SaleGateRequestDto
    {
        public string TransCode { get; set; }

        public SaleGateRequestDto()
        {
            Id = Guid.NewGuid();
            CreatedDate = DateTime.Now;
        }
        public Guid Id { get; set; }     
        public decimal TransAmount { get; set; }
        public decimal FirstAmount { get; set; }
        public decimal TopupAmount { get; set; }
        public SaleRequestStatus Status { get; set; }
        public DateTime CreatedDate { get; set; }             
        public string Mobile { get; set; }
        public string Vender { get; set; }
        public string Provider { get; set; }
        public string FirstProvider { get; set; }
        public string TopupProvider { get; set; }
        public string ServiceCode { get; set; }
        public string CategoryCode { get; set; }
        public string ProductCode { get; set; }
        public DateTime? EndDate { get; set; }
        public string Type { get; set; }
        public string ChartId { get; set; }
    }
}
