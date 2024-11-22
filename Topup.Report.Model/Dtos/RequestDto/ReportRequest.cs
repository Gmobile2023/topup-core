using System;
using System.Collections.Generic;
using Topup.Shared;
using ServiceStack;

namespace Topup.Report.Model.Dtos.RequestDto;



[Route("/api/v1/report/test-send-tele", "GET")]
public class TestSendTele : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public string ServiceCode { get; set; }
    public string TransCode { get; set; }
    public string Filter { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string AccountCode { get; set; }
    public string LoginCode { get; set; }
    public int AccountType { get; set; }

    public string File { get; set; }
}


[Route("/api/v1/report/ReportDetail", "GET")]
public class ReportDetailRequest : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public string ServiceCode { get; set; }
    public string TransCode { get; set; }
    public string Filter { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string AccountCode { get; set; }
    public string LoginCode { get; set; }
    public int AccountType { get; set; }

    public string File { get; set; }
}

[Route("/api/v1/report/ReportTransDetail", "GET")]
public class ReportTransDetailRequest : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public string Filter { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string RequestTransCode { get; set; }
    public string ReceivedAccount { get; set; }
    public string ServiceCode { get; set; }
    public string CategoryCode { get; set; }
    public string ProductCode { get; set; }
    public string ProviderCode { get; set; }
    public string UserProcess { get; set; }
    public int Status { get; set; }
    public string AccountCode { get; set; }   
    public int Type { get; set; }
    public string File { get; set; }
}

[Route("/api/v1/report/ReportTotalDay", "GET")]
public class ReportTotalDayRequest : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string AccountCode { get; set; }
}

[Route("/api/v1/report/ReportDebtDetail", "GET")]
public class ReportDebtDetailRequest : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string TransCode { get; set; }
    public string ServiceCode { get; set; }
    public string AccountCode { get; set; }
    public string LoginCode { get; set; }
    public int AccountType { get; set; }

    public string File { get; set; }
}

[Route("/api/v1/report/ReportTotalDebt", "GET")]
public class ReportTotalDebtRequest : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string AccountCode { get; set; }
    public string LoginCode { get; set; }
    public int AccountType { get; set; }
}

[Route("/api/v1/report/ReportRefundDetail", "GET")]
public class ReportRefundDetailRequest : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }

    /// <summary>
    ///     Đại lý
    /// </summary>
    public string AgentCode { get; set; }

    /// <summary>
    ///     Mã giao dịch
    /// </summary>
    public string TransCode { get; set; }


    /// <summary>
    ///     Mã giao dịch gốc
    /// </summary>
    public string TransCodeSouce { get; set; }

    /// <summary>
    ///     Dịch vụ
    /// </summary>
    public List<string> ServiceCode { get; set; }

    /// <summary>
    ///     Loại sản phẩm
    /// </summary>
    public List<string> CategoryCode { get; set; }

    /// <summary>
    ///     Sản phẩm
    /// </summary>
    public List<string> ProductCode { get; set; }

    public string LoginCode { get; set; }
    public int AccountType { get; set; }
}

[Route("/api/v1/report/ReportTransferDetail", "GET")]
public class ReportTransferDetailRequest : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }

    /// <summary>
    ///     Loại đại lý
    /// </summary>
    public int AgentType { get; set; }

    /// <summary>
    ///     Đại lý
    /// </summary>
    public string AgentTransferCode { get; set; }

    public string AgentReceiveCode { get; set; }

    /// <summary>
    ///     Mã giao dịch
    /// </summary>
    public string TransCode { get; set; }

    public string LoginCode { get; set; }
    public int AccountType { get; set; }
}

[Route("/api/v1/report/ReportDepositDetail", "GET")]
public class ReportDepositDetailRequest : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }

    /// <summary>
    ///     Loại đại lý
    /// </summary>
    public int AgentType { get; set; }

    /// <summary>
    ///     Đại lý
    /// </summary>
    public string AgentCode { get; set; }

    /// <summary>
    ///     Mã giao dịch
    /// </summary>
    public string TransCode { get; set; }

    public string LoginCode { get; set; }
}

[Route("/api/v1/report/ReportServiceDetail", "GET")]
public class ReportServiceDetailRequest : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }

    /// <summary>
    ///     Loại đại lý
    /// </summary>
    public int AgentType { get; set; }

    /// <summary>
    ///     Đại lý
    /// </summary>
    public string AgentCode { get; set; }

    /// <summary>
    ///     Đại lý tổng
    /// </summary>
    public string AgentCodeParent { get; set; }

    /// <summary>
    ///     Người thực hiện
    /// </summary>
    public string UserAgentStaffCode { get; set; }

    /// <summary>
    ///     Số thụ hưởng
    /// </summary>
    public string ReceivedAccount { get; set; }

    /// <summary>
    ///     Nhân viên sale
    /// </summary>
    public string UserSaleCode { get; set; }

    /// <summary>
    ///     Sale Leader
    /// </summary>
    public string UserSaleLeaderCode { get; set; }

    /// <summary>
    ///     Mã giao dịch NT sinh ra
    /// </summary>
    public string TransCode { get; set; }

    /// <summary>
    ///     Mã giao dịch đối tác gọi sang NT
    /// </summary>
    public string RequestRef { get; set; }


    /// <summary>
    ///     Mã giao dịch NT gọi sang NCC
    /// </summary>
    public string PayTransRef { get; set; }

    /// <summary>
    ///     Nhà cung cấp
    /// </summary>
    public List<string> VenderCode { get; set; }

    /// <summary>
    ///     Dịch vụ
    /// </summary>
    public List<string> ServiceCode { get; set; }

    /// <summary>
    ///     Loại sản phẩm
    /// </summary>
    public List<string> CategoryCode { get; set; }

    /// <summary>
    ///     Sản phẩm
    /// </summary>
    public List<string> ProductCode { get; set; }

    /// <summary>
    ///     Tỉnh/TP
    /// </summary>
    public int CityId { get; set; }

    /// <summary>
    ///     Quận huyện
    /// </summary>
    public int DistrictId { get; set; }

    /// <summary>
    ///     Phường/xã
    /// </summary>
    public int WardId { get; set; }

    /// <summary>
    ///     Trạng thái
    /// </summary>
    public int Status { get; set; }

    public string LoginCode { get; set; }
    public int AccountType { get; set; }

    public string File { get; set; }

    public string ReceiverType { get; set; }
    public string ProviderReceiverType { get; set; }
    public string ProviderTransCode { get; set; }

    public string ParentProvider { get; set; }

}

[Route("/api/v1/report/ReportServiceTotal", "GET")]
public class ReportServiceTotalRequest : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }

    /// <summary>
    ///     Dịch vụ
    /// </summary>
    public List<string> ServiceCode { get; set; }

    /// <summary>
    ///     Loại sản phẩm
    /// </summary>
    public List<string> CategoryCode { get; set; }

    /// <summary>
    ///     Sản phẩm
    /// </summary>
    public List<string> ProductCode { get; set; }

    public string LoginCode { get; set; }
    public int AccountType { get; set; }

    /// <summary>
    ///     Loại đại lý
    /// </summary>
    public int AgentType { get; set; }

    /// <summary>
    ///     Đại lý
    /// </summary>
    public string AgentCode { get; set; }

    public string ReceiverType { get; set; }
    public string ProviderReceiverType { get; set; }
}

[Route("/api/v1/report/ReportServiceProvider", "GET")]
public class ReportServiceProviderRequest : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }

    /// <summary>
    ///     Đại lý
    /// </summary>
    public string AgentCode { get; set; }

    /// <summary>
    ///     Đại lý tổng
    /// </summary>
    public string AgentCodeParent { get; set; }

    /// <summary>
    ///     Loại đại lý
    /// </summary>
    public int AgentType { get; set; }

    /// <summary>
    ///     Dịch vụ
    /// </summary>
    public List<string> ServiceCode { get; set; }

    /// <summary>
    ///     Loại sản phẩm
    /// </summary>
    public List<string> CategoryCode { get; set; }

    /// <summary>
    ///     Sản phẩm
    /// </summary>
    public List<string> ProductCode { get; set; }

    /// <summary>
    ///     Nhà cung cấp
    /// </summary>
    public List<string> ProviderCode { get; set; }

    public string LoginCode { get; set; }
    public int AccountType { get; set; }
    public string ReceiverType { get; set; }
    public string ProviderReceiverType { get; set; }
}

[Route("/api/v1/report/ReportComparePartner", "GET")]
public class ReportComparePartnerRequest : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }

    /// <summary>
    ///     Đại ly
    /// </summary>
    public string AgentCode { get; set; }

    public string ChangerType { get; set; }

    public string ServiceCode { get; set; }

    public string Type { get; set; }
}

[Route("/api/v1/report/SendMailComparePartner", "POST")]
public class SendMailComparePartnerRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }

    /// <summary>
    ///     Đại ly
    /// </summary>
    public string AgentCode { get; set; }

    public string Email { get; set; }
    public bool IsAuto { get; set; }
}

[Route("/api/v1/report/ReportAgentBalance", "GET")]
public class ReportAgentBalanceRequest : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }

    /// <summary>
    ///     Loại đại lý
    /// </summary>
    public int AgentType { get; set; }

    /// <summary>
    ///     Đại lý
    /// </summary>
    public string AgentCode { get; set; }

    /// <summary>
    ///     Nhân viên sale
    /// </summary>
    public string UserSaleCode { get; set; }

    /// <summary>
    ///     Sale Leader
    /// </summary>
    public string UserSaleLeaderCode { get; set; }

    public string LoginCode { get; set; }
    public int AccountType { get; set; }
}

[Route("/api/v1/report/ReportRevenueAgent", "GET")]
public class ReportRevenueAgentRequest : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }

    /// <summary>
    ///     Loại đại lý
    /// </summary>
    public int AgentType { get; set; }

    /// <summary>
    ///     Đại lý
    /// </summary>
    public string AgentCode { get; set; }

    /// <summary>
    ///     Nhân viên quản lý
    /// </summary>
    public string UserSaleCode { get; set; }

    /// <summary>
    ///     Sale Leader
    /// </summary>
    public string UserSaleLeaderCode { get; set; }

    /// <summary>
    ///     Dịch vụ
    /// </summary>
    public List<string> ServiceCode { get; set; }

    /// <summary>
    ///     Loại sản phẩm
    /// </summary>
    public List<string> CategoryCode { get; set; }

    /// <summary>
    ///     Sản phẩm
    /// </summary>
    public List<string> ProductCode { get; set; }

    /// <summary>
    ///     Lấy dữ liệu theo tỉnh
    /// </summary>
    public int CityId { get; set; }

    public string LoginCode { get; set; }
    public int AccountType { get; set; }
}

[Route("/api/v1/report/ReportRevenueCity", "GET")]
public class ReportRevenueCityRequest : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }

    /// <summary>
    ///     Loại đại lý
    /// </summary>
    public int AgentType { get; set; }

    /// <summary>
    ///     Nhân viên quản lý
    /// </summary>
    public string UserSaleCode { get; set; }

    /// <summary>
    ///     Sale Leader
    /// </summary>
    public string UserSaleLeaderCode { get; set; }

    /// <summary>
    ///     Tỉnh/TP
    /// </summary>
    public int CityId { get; set; }

    /// <summary>
    ///     Quận huyện
    /// </summary>
    public int DistrictId { get; set; }

    /// <summary>
    ///     Phường/xã
    /// </summary>
    public int WardId { get; set; }

    /// <summary>
    ///     Dịch vụ
    /// </summary>
    public List<string> ServiceCode { get; set; }

    /// <summary>
    ///     Loại sản phẩm
    /// </summary>
    public List<string> CategoryCode { get; set; }

    /// <summary>
    ///     Sản phẩm
    /// </summary>
    public List<string> ProductCode { get; set; }

    public string LoginCode { get; set; }
    public int AccountType { get; set; }
}

[Route("/api/v1/report/ReportTotalSaleAgent", "GET")]
public class ReportTotalSaleAgentRequest : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }

    /// <summary>
    ///     Loại đại lý
    /// </summary>
    public int AgentType { get; set; }

    /// <summary>
    ///     Đại lý
    /// </summary>
    public string AgentCode { get; set; }

    /// <summary>
    ///     Tỉnh/TP
    /// </summary>
    public int CityId { get; set; }

    /// <summary>
    ///     Quận huyện
    /// </summary>
    public int DistrictId { get; set; }

    /// <summary>
    ///     Phường/xã
    /// </summary>
    public int WardId { get; set; }

    /// <summary>
    ///     Nhân viên quản lý
    /// </summary>
    public string UserSaleCode { get; set; }

    /// <summary>
    ///     Sale Leader
    /// </summary>
    public string UserSaleLeaderCode { get; set; }

    /// <summary>
    ///     Dịch vụ
    /// </summary>
    public List<string> ServiceCode { get; set; }

    /// <summary>
    ///     Loại sản phẩm
    /// </summary>
    public List<string> CategoryCode { get; set; }

    /// <summary>
    ///     Sản phẩm
    /// </summary>
    public List<string> ProductCode { get; set; }

    public string LoginCode { get; set; }
    public int AccountType { get; set; }
}

[Route("/api/v1/report/ReportRevenueActive", "GET")]
public class ReportRevenueActiveRequest : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }

    /// <summary>
    ///     Loại đại lý
    /// </summary>
    public int AgentType { get; set; }

    /// <summary>
    ///     Đại lý
    /// </summary>
    public string AgentCode { get; set; }

    /// <summary>
    ///     Nhân viên quản lý
    /// </summary>
    public string UserSaleCode { get; set; }

    /// <summary>
    ///     Sale Leader
    /// </summary>
    public string UserSaleLeaderCode { get; set; }

    /// <summary>
    ///     Tỉnh/TP
    /// </summary>
    public int CityId { get; set; }

    /// <summary>
    ///     Quận huyện
    /// </summary>
    public int DistrictId { get; set; }

    /// <summary>
    ///     Phường/xã
    /// </summary>
    public int WardId { get; set; }

    /// <summary>
    ///     Trang thái
    /// </summary>
    public int Status { get; set; }

    public string LoginCode { get; set; }
    public int AccountType { get; set; }
}

[Route("/api/v1/report/RevenueInDay", "GET")]
public class RevenueInDayRequest
{
    public string AccountCode { get; set; }
}

[Route("/api/v1/report/TransDetailByTransCode", "GET")]
public class TransDetailByTransCodeRequest
{
    public string TransCode { get; set; }

    public string Type { get; set; }
}

[Route("/api/v1/report/BalanceTotal", "GET")]
public class BalanceTotalRequest : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string AccountCode { get; set; }

    public int AgentType { get; set; }
    public string LoginCode { get; set; }
    public int AccountType { get; set; }
}

[Route("/api/v1/report/BalanceGroupTotal", "GET")]
public class BalanceGroupTotalRequest : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

[Route("/api/v1/report/SyncTransRequest", "GET")]
public class SyncTransRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string AccountCode { get; set; }
    public bool IsOverride { get; set; }
}

[Route("/api/v1/report/SyncTotalDayRequest", "GET")]
public class SyncTotalDayRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string AccountCode { get; set; }
    public string CurrencyCode { get; set; }

    /// <summary>
    ///     1: Lịch sửa giao dịch,2: từ báo cáo chi tiết
    /// </summary>
    public int SourceType { get; set; }

    public bool IsOverride { get; set; }
}

[Route("/api/v1/report/SyncAccountRequest", "POST")]
public class SyncAccountRequest
{
    public long UserId { get; set; }
    public string AccountCode { get; set; }
}

[Route("/api/v1/report/SyncRegisterRequest", "POST")]
public class SyncRegisterRequest
{
    public string Name { get; set; }
    public string Code { get; set; }
    public string Content { get; set; }
    public string EmailSend { get; set; }
    public string EmailCC { get; set; }
    public bool IsAuto { get; set; }
    public string AccountList { get; set; }
    public string Providers { get; set; }
    public string Extend { get; set; }
    public int Total { get; set; }
}

[Route("/api/v1/report/RegisterRequest", "GET")]
public class GetRegisterRequest
{
    public string Code { get; set; }
}

[Route("/api/v1/report/SyncUpdateInfoTransRequest", "GET")]
public class SyncUpdateInfoTransRequest
{
    /// <summary>
    ///     Type = 1 .Đồng bộ thông tin chi tiết giao dịch
    ///     Type = 2 .Đồng bộ thông tin dữ liệu bảng tổng hợp
    ///     Type = 3 .Đồng bộ thông tin tài khoản
    /// </summary>
    public int Type { get; set; }

    public string ProductCode { get; set; }

    public int Quantity { get; set; }

    public string StockCode { get; set; }

    public DateTime Date { get; set; }

    public DateTime Date2 { get; set; }
    public string Description { get; set; }
}

[Route("/api/v1/report/SyncInfoObjectRequest", "GET")]
public class SyncInfoObjectRequest
{
    /// <summary>
    ///     Type = 1 .Thiếu giao dịch
    ///     Type = 2 .Thiếu số dư và số tiền thanh toán
    ///     Type = 3 .Thiếu mã TopupGate
    ///     Type = 4 .Đồng bộ danh sách mã thiếu tham chiếu
    /// </summary>
    public int Type { get; set; }

    public string TransCode { get; set; }
    public string ProviderCode { get; set; }
    public int Status { get; set; }
    public DateTime? Date { get; set; }
    public DateTime? ToDate { get; set; }
}

[Route("/api/v1/report/SyncQueryStockRequest", "GET")]
public class SyncQueryStockRequest
{
}

[Route("/api/v1/report/Sync_NXTProviderRequest", "GET")]
public class SyncNXTProviderRequest
{
    public string StockCode { get; set; }
    public long CardValue { get; set; }
    public string ProviderCode { get; set; }
    public string ProductCode { get; set; }
    public string CategoryCode { get; set; }
    public string ServiceCode { get; set; }
    public string StockType { get; set; }
    public long Increase { get; set; }
    public long Decrease { get; set; }
    public long InventoryBefore { get; set; }
    public long InventoryAfter { get; set; }
    public long IncreaseSupplier { get; set; }
    public long IncreaseOther { get; set; }
    public long Sale { get; set; }
    public long ExportOther { get; set; }
    public byte Status { get; set; }
    public DateTime CreatedDate { get; set; }
    public string ShortDate { get; set; }
}

[Route("/api/v1/report/CheckBalanceSupplierRequest", "GET")]
public class CheckBalanceSupplierRequest
{
    public string Providercode { get; set; }
}

public class BalanceTotalItem
{
    public string AccountCode { get; set; }
    public string AccountType { get; set; }
    public DateTime CreateDate { get; set; }
    public double Credited { get; set; }
    public double Debit { get; set; }
    public double BalanceBefore { get; set; }
    public double BalanceAfter { get; set; }
}

[Route("/api/v1/report/cardstock/card_stock_histories", "GET")]
public class CardStockHistoriesRequest : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public int CardValue { get; set; }
    public string Vendor { get; set; }
    public string StockCode { get; set; }
    public string ProductCode { get; set; }
    public string CategoryCode { get; set; }
    public string StockType { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

[Route("/api/v1/report/cardstock/card_stock_ImExPort", "GET")]
public class CardStockImExPortRequest : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public string Filter { get; set; }
    public string StoreCode { get; set; }
    public string ServiceCode { get; set; }
    public string CategoryCode { get; set; }
    public string ProductCode { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

[Route("/api/v1/report/cardstock/card_stock_imexportprovider", "GET")]
public class CardStockImExPortProviderRequest : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public string Filter { get; set; }
    public string StoreCode { get; set; }
    public string ServiceCode { get; set; }
    public string CategoryCode { get; set; }
    public string ProductCode { get; set; }
    public string ProviderCode { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

[Route("/api/v1/report/cardstock/card_stock_Auto", "GET")]
public class CardStockAutoRequest : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
}

[Route("/api/v1/report/cardstock/card_stock_inventory", "GET")]
public class CardStockInventoryRequest : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public int CardValue { get; set; }
    public string Vendor { get; set; }
    public string ProductCode { get; set; }
    public string CategoryCode { get; set; }
    public string StockType { get; set; }
    public string StockCode { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

[Route("/api/v1/stock/stock_get_list")]
public class CardStockGetListRequest : Pagination
{
    public string StockCode { get; set; }
    public int Status { get; set; }
    public string Vendor { get; set; }
    public int CardValue { get; set; }
    public string ProductCode { get; set; }
    public string CategoryCode { get; set; }
    public string StockType { get; set; }
}

[Route("/api/v1/report/ReportRevenueDashBoardDay", "GET")]
public class ReportRevenueDashBoardDayRequest : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }

    public int AccountType { get; set; }
    public string LoginCode { get; set; }

    public List<string> ServiceCode { get; set; }
    public List<string> CategoryCode { get; set; }
    public List<string> ProductCode { get; set; }
}

[Route("/api/v1/report/ReportAgentGeneralDash", "GET")]
public class ReportAgentGeneralDashRequest : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string LoginCode { get; set; }
    public List<string> ServiceCode { get; set; }
    public List<string> CategoryCode { get; set; }
    public List<string> ProductCode { get; set; }
    public string AgentCode { get; set; }
}

public class ReportTotalAuto0hRequest : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
}

public class ReportBalanceSupplierRequest : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }

    public string Providers { get; set; }
}

public class ReportSmsRequest : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
}

[Route("/api/v1/report/SyncAgentBalanceRequest", "GET")]
public class SyncAgentBalanceRequest
{
    /// <summary>
    ///     Type = 1 .Check tài khoản
    ///     Type = 2 .Đồng bộ số liệu
    /// </summary>
    public int Type { get; set; }

    public string AgentCode { get; set; }

    public DateTime FromDate { get; set; }

    public DateTime ToDate { get; set; }
}

[Route("/api/v1/report/SyncExportBatchRequest", "GET")]
public class SyncExportBatchRequest
{
    public DateTime Date { get; set; }
}

[Route("/api/v1/report/ReportTopupRequestLogs", "GET")]
public class ReportTopupRequestLogs : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string TransCode { get; set; }
    public string TransRef { get; set; }
    public string TransIndex { get; set; }
    public string ProviderCode { get; set; }
    public string PartnerCode { get; set; }
    public List<string> ServiceCode { get; set; }
    public List<string> CategoryCode { get; set; }
    public List<string> ProductCode { get; set; }
    public int Status { get; set; }
    public string File { get; set; }
}

[Route("/api/v1/backend/topup", "GET")]
public class CallBackEndRequest 
{
    public string Filter { get; set; }    
}
