using System;

namespace HLS.Paygate.Shared.Helpers;

public static class TelcoHepper
{
    public static string ConvertTelco(string telcoCheck)
    {
        return telcoCheck switch
        {
            TelcoCheckConst.Viettel => VendorConst.Viettel,
            TelcoCheckConst.MobiPhone => VendorConst.MobiPhone,
            TelcoCheckConst.VinaPhone => VendorConst.VinaPhone,
            TelcoCheckConst.VietnamMobile => VendorConst.VietnamMobile,
            TelcoCheckConst.Gmobile => VendorConst.Gmobile,
            _ => null
        };
    }

    public static string GetVendorTrans(string serviceCode, string productCode)
    {
        try
        {
            string code;
            if (serviceCode == ServiceCodes.TOPUP || serviceCode.StartsWith("PIN_") ||
                serviceCode == ServiceCodes.TOPUP_DATA)
                code = productCode.Split("_")[0];
            else if (serviceCode == ServiceCodes.PAY_BILL)
                code = productCode;
            else
                code = productCode;

            return code;
        }
        catch (Exception)
        {
            return productCode;
        }
    }
}