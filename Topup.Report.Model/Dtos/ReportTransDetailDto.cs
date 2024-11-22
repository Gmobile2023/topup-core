using System;
using Topup.Shared;

namespace Topup.Report.Model.Dtos;

public class ReportTransDetailDto
{
    public string StatusName { get; set; }
    public ReportStatus Status { get; set; }
    public string TransType { get; set; }
    public string ServiceCode { get; set; }

    public string CategoryCode { get; set; }
    public string TransTypeName { get; set; }
    public string Vender { get; set; }
    public decimal Amount { get; set; }
    public decimal Quantity { get; set; }

    public decimal Fee { get; set; }

    public decimal Discount { get; set; }
    public decimal Price { get; set; }
    public decimal PriceIn { get; set; }
    public decimal PriceOut { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal Balance { get; set; }
    public string AccountCode { get; set; }
    public string AccountInfo { get; set; }
    public string AccountRef { get; set; }
    public string TransCode { get; set; }  
    public string UserProcess { get; set; }
    public DateTime CreatedDate { get; set; }
    public string RequestTransSouce { get; set; }
    public string TransTransSouce { get; set; }     
    public string TransNote { get; set; }
}

public class UserInfoDto
{
    public int Id { get; set; }

    public string Surname { get; set; }

    public string UserName { get; set; }

    public string PhoneNumber { get; set; }

    public string FullName { get; set; }

    public string AccountCode { get; set; }

    public string ParentCode { get; set; }

    public string TreePath { get; set; }

    public int AccountType { get; set; }

    public int AgentType { get; set; }

    public int NetworkLevel { get; set; }

    public string AgentName { get; set; }

    public string EmailAddress { get; set; }

    public UserUnit Unit { get; set; }

    public int? UserSaleLeadId { get; set; }
    public string SaleCode { get; set; }
    public string LeaderCode { get; set; }

    public DateTime? CreationTime { get; set; }
}

public class UserInfoPeriodDto
{
    public long Id { get; set; }
    public string AgentCode { get; set; }
    public string UserName { get; set; }
    public string PhoneNumber { get; set; }
    public string EmailAddress { get; set; }
    public string FullName { get; set; }
    public int AgentType { get; set; }
    public int Period { get; set; }
    public string ContractNumber { get; set; }
    public string EmailReceives { get; set; }
    public string FolderFtp { get; set; }
    public DateTime? SigDate { get; set; }
}

public class UserUnit
{
    public int CityId { get; set; }
    public string CityName { get; set; }
    public string CityCode { get; set; }
    public int DistrictId { get; set; }
    public string DistrictName { get; set; }
    public string DistrictCode { get; set; }
    public int WardId { get; set; }
    public string WardName { get; set; }
    public string WardCode { get; set; }
    public string IdIdentity { get; set; }
    public string ChatId { get; set; }
}

public class ServiceInfoDto
{
    public int Id { get; set; }
    public string ServiceCode { get; set; }
    public string ServicesName { get; set; }
}

public class ProductInfoDto
{
    public int CategoryId { get; set; }
    public int Id { get; set; }
    public int ProductType { get; set; }
    public decimal ProductValue { get; set; }
    public string CategoryCode { get; set; }
    public string CategoryName { get; set; }
    public string ProductCode { get; set; }
    public string ProductName { get; set; }
    public int ServiceId { get; set; }
    public string ServiceCode { get; set; }
    public string ServiceName { get; set; }
}

public class ProviderInfoDto
{
    public int Id { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
}

public class UserLimitDebtDto
{
    public decimal Limit { get; set; }

    public int DebtAge { get; set; }
}

public class UserInfoSaleDto
{
    public string SaleCode { get; set; }
    public string SaleLeaderCode { get; set; }
    public long UserSaleId { get; set; }
    public long UserLeaderId { get; set; }
}

public class ReportDetailDto
{
    public int Index { get; set; }
    public string TransCode { get; set; }
    public string ServiceCode { get; set; }
    public string ServiceName { get; set; }
    public DateTime CreatedDate { get; set; }
    public decimal BalanceBefore { get; set; }
    public decimal Increment { get; set; }
    public decimal Decrement { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public string TransNote { get; set; }
    public string SrcAccountCode { get; set; }
    public string DesAccountCode { get; set; }
    public string Description { get; set; }

    public decimal SrcAccountBalanceAfterTrans { get; set; }

    public decimal DesAccountBalanceAfterTrans { get; set; }

    public decimal SrcAccountBalanceBeforeTrans { get; set; }

    public decimal DesAccountBalanceBeforeTrans { get; set; }

    public decimal SrcAccountBalance { get; set; }
    public decimal DesAccountBalance { get; set; }
}