using System;
using HLS.Paygate.Gw.Model.Enums;

namespace HLS.Paygate.Gw.Model.Commands
{
    public interface CardCallBackCommand : ICommand
    {
        public string TransCode { get; set; }
        public string TransRef { get; set; }
        public string Vendor { get; }
        public int CardValue { get; }
        public string Serial { get; set; }
        public string CardCode { get; set; }
        public string ResponseCode { get; set; }
        public string ResponseMessage { get; }
        public decimal AmountReceived { get; }
        public int RequestValue { get; set; }
    }
}