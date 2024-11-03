using MassTransit.Futures.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GMB.Topup.TopupGw.Components.Connectors.VTC365
{
    public class Request365Request
    {
        public string partnerCode { get; set; }

        public string categoryID { get; set; }

        public string productID { get; set; }

        public string customerID { get; set; }

        public string partnerTransID { get; set; }

        public string partnerTransDate { get; set; }

        public string productAmount { get; set; }

        public string data { get; set; }

        public string dataSign { get; set; }

    }

    public class Vtc365Response
    {
        public int responseCode { get; set; }

        public int status { get; set; }

        public string partnerTransID { get; set; }

        public string description { get; set; }

        public decimal balance { get; set; }

        public string dataInfo { get; set; }

        public string dataSign { get; set; }
    }

    internal class CardData
    {
        public string orderID { get; set; }

        public List<CardItem> ListCard { get; set; }
    }

    internal class CardItem
    {
        public string Serial { get; set; }
        public string Code { get; set; }
        public string ExpriredDate { get; set; }
        public decimal Value { get; set; }

    }

    internal class BillItem
    {
        public string productID { get; set; }

        public List<billItem> bills { get; set; }

        public customerItem customer { get; set; }

    }

    internal class billItem
    {
        public string bill_number { get; set; }

        public string period { get; set; }

        public string amount { get; set; }

        public string bill_type { get; set; }

        public string bill_name { get; set; }
        public string amount_and_fee { get; set; }
    }

    internal class customerItem
    {
        public string customer_code { get; set; }

        public string customer_name { get; set; }

        public string customer_address { get; set; }

        public string customer_smartcard_name { get; set; }

        public string customer_smartcard_serial { get; set; }

        public string customer_other { get; set; }

        public string customer_packages { get; set; }

    }

    internal class checkTranItem
    {
        public string OrderID { get; set; }

        public int status { get; set; }

        public int responseCode { get; set; }

        public int ProductID { get; set; }

        public int Quantity { get; set; }

        public string Message { get; set; }
    }

}
