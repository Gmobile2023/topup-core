using System;

namespace HLS.Paygate.Gw.Model.Commands
{
    public interface CollectDiscountCommand:ICommand
    {
        decimal Amount { get; set; }
        string TransCode { get; set; }
        string Description { get; set; }
        public string AccountCode { get; set; }
    }
}