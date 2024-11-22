using System;
using System.Collections.Generic;
using System.Linq;
using Topup.Shared.Dtos;

namespace Topup.Shared.Common;

public static class SaleCommon
{
    public static string GetProductCode(string serviceCode, string categoryCode, int amount)
    {
       
        if (serviceCode == ServiceCodes.TOPUP || serviceCode == ServiceCodes.TOPUP_DATA ||
            serviceCode.StartsWith("PIN_"))
        {
            if (amount % 1000 == 0)
            {
                var productAmount = amount / 1000;
                return categoryCode + "_" + productAmount;
            }
            return categoryCode + "____" + amount;
        }

        return null;
    }

    public static string GetPinCodeService(string categoryCode)
    {
        try
        {
            var pf = categoryCode.Split('_')[1];
            return pf switch
            {
                "PINCODE" => ServiceCodes.PIN_CODE,
                "PINDATA" => ServiceCodes.PIN_DATA,
                "GAME" => ServiceCodes.PIN_GAME,
                _ => null
            };
        }
        catch (Exception e)
        {
            return null;
        }
    }

    public static string GetTopupService(string categoryCode)
    {
        try
        {
            var pf = categoryCode.Split('_')[1];
            return pf switch
            {
                "TOPUPDATA" => ServiceCodes.TOPUP_DATA,
                "TOPUP" => ServiceCodes.TOPUP,
                _ => null
            };
        }
        catch (Exception e)
        {
            return null;
        }
    }

    public static InvoiceResultDto GetBillQueryInfo(InvoiceResponseDto response)
    {
        var info = new InvoiceResultDto
            {CustomerReference = response.CustomerReference, Amount = decimal.Parse(response.Amount)};
        if (!(response.InvoiceAttributes?.Count > 0)) return info;
        var checkName =
            response.InvoiceAttributes.FirstOrDefault(x => x.InvoiceAttributeTypeId == "CUS_NAME");
        if (checkName != null)
            info.CustomerName = checkName.Value;
        var checkAddress =
            response.InvoiceAttributes.FirstOrDefault(
                x => x.InvoiceAttributeTypeId == "CUS_ADDRESS");
        if (checkAddress != null)
            info.Address = checkAddress.Value;

        var checkPeriod =
            response.InvoiceAttributes.FirstOrDefault(x =>
                x.InvoiceAttributeTypeId == "INV_PERIOD");
        if (checkPeriod != null && !string.IsNullOrEmpty(checkPeriod.Value) &&
            checkPeriod.Value.Contains('|'))
            info.Period = checkPeriod.Value.Split('|')[1];
        else if (!string.IsNullOrEmpty(checkPeriod?.Value))
            info.Period = checkPeriod?.Value;

        info.PeriodDetails = new List<PeriodDto>
        {
            new()
            {
                Amount = info.Amount,
                Period = info.Period
            }
        };
        return info;
    }
}