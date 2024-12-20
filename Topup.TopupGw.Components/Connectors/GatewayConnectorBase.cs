﻿using System.Threading.Tasks;
using Topup.Shared;
using Topup.Shared.Dtos;
using Topup.TopupGw.Contacts.ApiRequests;
using Topup.TopupGw.Contacts.Dtos;
using Topup.TopupGw.Domains.BusinessServices;

namespace Topup.TopupGw.Components.Connectors;

public abstract class GatewayConnectorBase : IGatewayConnector
{
    protected readonly ITopupGatewayService TopupGatewayService;
    
    public GatewayConnectorBase(ITopupGatewayService topupGatewayService)
    {
        TopupGatewayService = topupGatewayService;
    }
    
    public virtual Task<MessageResponseBase> TopupAsync(TopupRequestLogDto topupRequestLog, ProviderInfoDto providerInfo)
    {
        throw new System.NotImplementedException();
    }

    public virtual Task<MessageResponseBase> TransactionCheckAsync(string providerCode, string transCodeToCheck, string transCode, string serviceCode = null,
        ProviderInfoDto providerInfo = null)
    {
        throw new System.NotImplementedException();
    }

    public virtual Task<NewMessageResponseBase<InvoiceResultDto>> QueryAsync(PayBillRequestLogDto payBillRequestLog)
    {
        throw new System.NotImplementedException();
    }

    public virtual Task<MessageResponseBase> CardGetByBatchAsync(CardRequestLogDto cardRequestLog)
    {
        throw new System.NotImplementedException();
    }

    public virtual Task<MessageResponseBase> CheckBalanceAsync(string providerCode, string transCode)
    {
        throw new System.NotImplementedException();
    }

    public virtual Task<MessageResponseBase> DepositAsync(DepositRequestDto request)
    {
        throw new System.NotImplementedException();
    }

    public virtual Task<MessageResponseBase> PayBillAsync(PayBillRequestLogDto payBillRequestLog)
    {
        throw new System.NotImplementedException();
    }

    public virtual Task<ResponseMessageApi<object>> GetProductInfo(GetProviderProductInfo info)
    {
        throw new System.NotImplementedException();
    }

    public virtual Task<MessageResponseBase> CheckPrePostPaid(string msisdn, string transCode, string providerCode)
    {
        throw new System.NotImplementedException();
    }
}