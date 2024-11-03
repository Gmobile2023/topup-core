using System;
using System.Collections.Generic;

namespace GMB.Topup.Report.Model.Dtos;

public class ReportRefundDetailDto
{
    #region 1.Đại lý

    public string AgentCode { get; set; }

    public string AgentInfo { get; set; }

    #endregion


    #region 2.Dịch vụ

    public string ServiceCode { get; set; }

    public string ServiceName { get; set; }

    #endregion

    #region 3.Loại sản phẩm

    public string CategoryCode { get; set; }

    public string CategoryName { get; set; }

    #endregion

    #region 4.Sản phẩm

    public string ProductCode { get; set; }

    public string ProductName { get; set; }

    #endregion

    #region 7.Thông tin về số tiền,mã giao dich, thời gian, trạng thái

    /// <summary>
    ///     Phí
    /// </summary>
    public decimal Fee { get; set; }

    /// <summary>
    ///     Chiết khấu
    /// </summary>
    public decimal Discount { get; set; }

    /// <summary>
    ///     Thành tiền
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    ///     Thời gian
    /// </summary>
    public DateTime CreatedTime { get; set; }

    /// <summary>
    ///     Mã giao dịch
    /// </summary>
    public string TransCode { get; set; }

    /// <summary>
    ///     Mã giao dịch gốc
    /// </summary>
    public string TransCodeSouce { get; set; }

    /// <summary>
    ///     Tên cửa hàng
    /// </summary>
    public string AgentName { get; set; }

    #endregion
}

public class ReportTransferDetailDto
{
    #region 1.Loại đại lý

    public string AgentTypeName { get; set; }

    public int AgentType { get; set; }

    #endregion

    #region 2.Đại lý

    public string AgentReceiveCode { get; set; }

    public string AgentReceiveInfo { get; set; }

    public string AgentTransfer { get; set; }

    public string AgentTransferInfo { get; set; }

    #endregion

    #region 3.Dịch vụ

    public string ServiceCode { get; set; }

    public string ServiceName { get; set; }

    #endregion

    #region 4.Thông tin về số tiền,mã giao dich, thời gian

    /// <summary>
    ///     Thời gian
    /// </summary>
    public DateTime CreatedTime { get; set; }

    /// <summary>
    ///     Thành tiền
    /// </summary>
    public decimal Price { get; set; }

    public string TransCode { get; set; }
    public string Messager { get; set; }

    #endregion
}

public class ReportDepositDetailDto
{
    #region 1.Loại đại lý

    public string AgentTypeName { get; set; }

    public int AgentType { get; set; }

    #endregion

    #region 2.Đại lý

    public string AgentCode { get; set; }

    public string AgentInfo { get; set; }

    #endregion

    #region 3.Dịch vụ

    public string ServiceCode { get; set; }

    public string ServiceName { get; set; }

    #endregion

    #region 4.Thông tin về số tiền,mã giao dich, thời gian

    /// <summary>
    ///     Thời gian
    /// </summary>
    public DateTime CreatedTime { get; set; }

    /// <summary>
    ///     Thành tiền
    /// </summary>
    public decimal Price { get; set; }

    public string TransCode { get; set; }
    public string Messager { get; set; }

    #endregion
}

public class ReportServiceDetailDto
{
    #region 1.Loại đại lý

    public string AgentTypeName { get; set; }

    public int AgentType { get; set; }

    #endregion

    #region 2.Đại lý

    public string AgentCode { get; set; }

    public string AgentInfo { get; set; }

    #endregion

    #region 2.1.NV Kinh Doanh

    public string StaffCode { get; set; }

    public string StaffInfo { get; set; }

    #endregion

    #region 3.Nhà cung cấp

    public string VenderCode { get; set; }

    public string VenderName { get; set; }

    #endregion

    #region 4.Dịch vụ

    public string ServiceCode { get; set; }

    public string ServiceName { get; set; }

    #endregion

    #region 5.Loại sản phẩm

    public string CategoryCode { get; set; }

    public string CategoryName { get; set; }

    #endregion

    #region 6.Sản phẩm

    public string ProductCode { get; set; }

    public string ProductName { get; set; }

    #endregion

    #region 7.Thông tin về số tiền,mã giao dich, thời gian, trạng thái

    /// <summary>
    ///     Đơn giá
    /// </summary>
    public decimal Value { get; set; }

    /// <summary>
    ///     Số lượng
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    ///     Số tiền chiết khấu
    /// </summary>
    public decimal Discount { get; set; }

    /// <summary>
    ///     Phần tiền phí
    /// </summary>
    public decimal Fee { get; set; }

    /// <summary>
    ///     Thành tiền
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    ///     Hoa hồng đại lý tổng
    /// </summary>
    public decimal CommistionAmount { get; set; }

    /// <summary>
    ///     Thông tin đại lý tổng
    /// </summary>
    public string AgentParentInfo { get; set; }

    /// <summary>
    ///     Số thụ hưởng
    /// </summary>
    public string ReceivedAccount { get; set; }

    /// <summary>
    ///     Thời gian
    /// </summary>
    public DateTime CreatedTime { get; set; }

    /// <summary>
    ///     Người thực hiện
    /// </summary>
    public string UserProcess { get; set; }

    /// <summary>
    ///     Mã giao dịch NT sinh ra
    /// </summary>
    public string TransCode { get; set; }

    /// <summary>
    ///     Mã giao dịch đối tác gọi sang
    /// </summary>
    public string RequestRef { get; set; }

    /// <summary>
    ///     Mã giao dịch NT gọi sang đối tác
    /// </summary>
    public string PayTransRef { get; set; }


    public int Status { get; set; }

    /// <summary>
    ///     Trạng thái
    /// </summary>
    public string StatusName { get; set; }

    public string Channel { get; set; }

    public string ReceiverType { get; set; }
    public string ProviderReceiverType { get; set; }
    public string ProviderTransCode { get; set; }
    public string ParentProvider { get; set; }

    public string ReceiverTypeNote { get; set; }

    #endregion
}

public class ReportServiceTotalDto
{
    #region 1.Dịch vụ

    public string ServiceCode { get; set; }

    public string ServiceName { get; set; }

    #endregion

    #region 2.Loại sản phẩm

    public string CategoryCode { get; set; }

    public string CategoryName { get; set; }

    #endregion

    #region 3.Sản phẩm

    public string ProductCode { get; set; }

    public string ProductName { get; set; }

    public decimal OrderValue { get; set; }

    #endregion

    #region 4.Thông tin về số tiền

    /// <summary>
    ///     Đơn giá
    /// </summary>
    public decimal Value { get; set; }

    /// <summary>
    ///     Số lượng
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    ///     Số tiền chiết khấu
    /// </summary>
    public decimal Discount { get; set; }

    /// <summary>
    ///     Phí
    /// </summary>
    public decimal Fee { get; set; }

    /// <summary>
    ///     Thành tiền
    /// </summary>
    public decimal Price { get; set; }

    #endregion
}

public class ReportServiceTotalProviderDto
{
    #region 0.Nhà cung cấp

    public string ProviderCode { get; set; }

    public string ProviderName { get; set; }

    #endregion

    #region 1.Dịch vụ

    public string ServiceCode { get; set; }

    public string ServiceName { get; set; }

    #endregion

    #region 2.Loại sản phẩm

    public string CategoryCode { get; set; }

    public string CategoryName { get; set; }

    #endregion

    #region 3.Sản phẩm

    public string ProductCode { get; set; }

    public string ProductName { get; set; }

    public decimal OrderValue { get; set; }

    #endregion

    #region 4.Thông tin về số tiền

    /// <summary>
    ///     Đơn giá
    /// </summary>
    public decimal Value { get; set; }

    /// <summary>
    ///     Số lượng
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    ///     Số tiền chiết khấu
    /// </summary>
    public decimal Discount { get; set; }

    /// <summary>
    ///     Phí
    /// </summary>
    public decimal Fee { get; set; }

    /// <summary>
    ///     Thành tiền
    /// </summary>
    public decimal Price { get; set; }

    #endregion
}

public class ReportAgentBalanceDto
{
    #region 1.Loại đại lý

    public int AgentType { get; set; }
    public string AgentTypeName { get; set; }

    #endregion

    #region 2.Đại lý

    public string AgentCode { get; set; }

    public string AgentInfo { get; set; }

    public string SaleCode { get; set; }

    public string SaleInfo { get; set; }

    public string SaleLeaderCode { get; set; }

    public string SaleLeaderInfo { get; set; }

    #endregion

    #region 3.Thông tin về số tiền

    /// <summary>
    ///     Đầu kỳ
    /// </summary>
    public double BeforeAmount { get; set; }

    /// <summary>
    ///     Tiền nạp
    /// </summary>
    public double InputAmount { get; set; }

    /// <summary>
    ///     Phát sinh tăng
    /// </summary>
    public double AmountUp { get; set; }

    /// <summary>
    ///     Bán hàng
    /// </summary>
    public double SaleAmount { get; set; }

    /// <summary>
    ///     Phát sinh giảm khác
    /// </summary>
    public double AmountDown { get; set; }

    /// <summary>
    ///     Số dư cuối kỳ
    /// </summary>
    public double AfterAmount { get; set; }

    #endregion
}

public class ReportRevenueAgentDto
{
    #region 1.Loại đại lý

    public string AgentTypeName { get; set; }

    public int AgentType { get; set; }

    #endregion

    #region 2.Đại lý

    public string AgentCode { get; set; }

    public string AgentInfo { get; set; }

    #endregion

    #region 3.Thông tin về số tiền

    public string AgentName { get; set; }

    public string SaleCode { get; set; }

    public string LeaderCode { get; set; }
    public string SaleInfo { get; set; }
    public string SaleLeaderInfo { get; set; }

    public string CityInfo { get; set; }

    public string DistrictInfo { get; set; }

    public string WardInfo { get; set; }

    public int CityId { get; set; }

    public int DistrictId { get; set; }

    public int WardId { get; set; }

    /// <summary>
    ///     Số lượng
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    ///     Số tiền chiết khấu
    /// </summary>
    public decimal Discount { get; set; }

    /// <summary>
    ///     Phí
    /// </summary>
    public decimal Fee { get; set; }

    /// <summary>
    ///     Thành tiền
    /// </summary>
    public decimal Price { get; set; }

    #endregion
}

public class ReportRevenueCityDto
{
    #region 1.Thông tin về số tiền

    public string CityInfo { get; set; }

    public string DistrictInfo { get; set; }

    public string WardInfo { get; set; }

    public int CityId { get; set; }

    public int DistrictId { get; set; }

    public int WardId { get; set; }

    public decimal QuantityAgent { get; set; }

    /// <summary>
    ///     Số lượng
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    ///     Số tiền chiết khấu
    /// </summary>
    public decimal Discount { get; set; }

    /// <summary>
    ///     Thành tiền
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    ///     Phí
    /// </summary>

    public decimal Fee { get; set; }

    public string AccountCode { get; set; }

    #endregion
}

public class ReportTotalSaleAgentDto
{
    #region 1.Loại đại lý

    public string AgentTypeName { get; set; }

    public int AgentType { get; set; }

    #endregion

    #region 2.Đại lý

    public string AgentCode { get; set; }

    public string AgentInfo { get; set; }

    #endregion

    #region 3.Thông tin về số tiền

    public string AgentName { get; set; }

    public string SaleCode { get; set; }
    public string SaleInfo { get; set; }
    public string LeaderCode { get; set; }
    public string SaleLeaderInfo { get; set; }

    public int CityId { get; set; }
    public string CityInfo { get; set; }
    public int DistrictId { get; set; }
    public string DistrictInfo { get; set; }

    public int WardId { get; set; }
    public string WardInfo { get; set; }

    /// <summary>
    ///     Số lượng
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    ///     Số tiền chiết khấu
    /// </summary>
    public decimal Discount { get; set; }

    /// <summary>
    ///     Phí
    /// </summary>
    public decimal Fee { get; set; }

    /// <summary>
    ///     Thành tiền
    /// </summary>
    public decimal Price { get; set; }

    #endregion
}

public class ReportAgentBalanceTemp : ReportAgentBalanceDto
{
    public DateTime MinDate { get; set; }

    public DateTime MaxDate { get; set; }
}

public class ReportRevenueActiveDto
{
    #region 1.Loại đại lý

    public string AgentTypeName { get; set; }

    public int AgentType { get; set; }

    #endregion

    #region 2.Đại lý

    public string AgentCode { get; set; }

    public string AgentInfo { get; set; }

    #endregion

    #region 3.Thông tin về số tiền

    public string AgentName { get; set; }

    public string SaleInfo { get; set; }

    public string SaleLeaderInfo { get; set; }

    public string CityInfo { get; set; }

    public string DistrictInfo { get; set; }

    public string WardInfo { get; set; }

    public int CityId { get; set; }


    public int DistrictId { get; set; }

    public int WardId { get; set; }

    public string IdIdentity { get; set; }

    /// <summary>
    ///     Số tiền nạp trong kỳ
    /// </summary>
    public decimal Deposit { get; set; }

    /// <summary>
    ///     Số tiền bán trong kỳ
    /// </summary>
    public decimal Sale { get; set; }

    public string Status { get; set; }

    #endregion
}

public class ReportRevenueTotalAutoDto
{
    #region *.Thông tin về số tiền

    public string AccountCode { get; set; }
    public DateTime CreatedDay { get; set; }

    /// <summary>
    ///     Số lượng tài khoản kích hoạt
    /// </summary>
    public int AccountActive { get; set; }

    /// <summary>
    ///     Số lượng agent hoạt động
    /// </summary>
    public int AccountRevenue { get; set; }

    /// <summary>
    ///     Số dư đầu kỳ
    /// </summary>
    public double Before { get; set; }

    /// <summary>
    ///     Nạp tiền trong kỳ
    /// </summary>
    public double InputDeposit { get; set; }

    /// <summary>
    ///     Phát sinh tăng khác
    /// </summary>
    public double IncOther { get; set; }

    /// <summary>
    ///     Số tiền bán trong kỳ
    /// </summary>
    public double Sale { get; set; }

    /// <summary>
    ///     Phát sinh giảm khác
    /// </summary>
    public double DecOther { get; set; }

    /// <summary>
    ///     Số dư cuối kỳ
    /// </summary>
    public double After { get; set; }

    #endregion
}

public class BalanceSupplierItem
{
    public string Name { get; set; }

    public decimal Balance { get; set; }
}

public class ReportBalanceSupplierDto
{
    public DateTime CreatedDay { get; set; }

    public List<BalanceSupplierItem> Items { get; set; }
}

public class ReportRevenueDashboardDay
{
    public DateTime CreatedDay { get; set; }
    public string DayText { get; set; }
    public decimal Revenue { get; set; }
    public decimal Discount { get; set; }
}

public class ReportRevenueCommistionDashDay
{
    public DateTime CreatedDay { get; set; }
    public string DayText { get; set; }
    public decimal Revenue { get; set; }
    public decimal Commission { get; set; }
}

public class ReportSmsDto
{
    public DateTime CreatedDate { get; set; }
    public string Phone { get; set; }
    public string Message { get; set; }
    public string TransCode { get; set; }
    public string Channel { get; set; }
    public int Status { get; set; }

    public string Result { get; set; }
    public int Id { get; set; }
}

public class ExportItemData
{
    public string CreatedTime { get; set; }
    public string TransCode { get; set; }
    public string RequestRef { get; set; }
    public string AgentType { get; set; }
    public string AgentCode { get; set; }
    public string Providers { get; set; }
    public string Services { get; set; }
    public string Categories { get; set; }
    public string Products { get; set; }
    public string RequestAmount { get; set; }
    public string Discounts { get; set; }
    public string Fees { get; set; }
    public string TotalAmount { get; set; }
    public string Phonenumber { get; set; }
    public string Staff { get; set; }
    public string TransCodePay { get; set; }
    public string Channel { get; set; }
    public string IsRefund { get; set; }

    public decimal Quantity { get; set; }
}

public class ReportComparePartnerDto
{
    #region 1.Dịch vụ

    public string ServiceCode { get; set; }
    public string ServiceName { get; set; }

    #endregion

    #region 2.Loại sản phẩm

    public string CategoryCode { get; set; }

    public string CategoryName { get; set; }

    #endregion

    #region 3.Sản phẩm

    public string ProductCode { get; set; }

    public string ProductName { get; set; }

    #endregion

    #region 4.Thông tin về số tiền

    /// <summary>
    ///     Đơn giá
    /// </summary>
    public decimal Value { get; set; }

    public decimal ProductValue { get; set; }

    /// <summary>
    ///     Số lượng
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    ///     Số tiền chiết khấu
    /// </summary>
    public decimal Discount { get; set; }

    public decimal DiscountRate { get; set; }

    public decimal Fee { get; set; }

    public string FeeText { get; set; }

    /// <summary>
    ///     Thành tiền
    /// </summary>
    public decimal Price { get; set; }

    public string Type { get; set; }

    public string ReceiverType { get; set; }
    public string Note { get; set; }

    #endregion
}

public class ReportBalancePartnerDto
{
    public int Index { get; set; }

    public string Name { get; set; }

    public double Value { get; set; }

    //nhannv:Thêm list Mã GD để gửi cảnh báo.
    public List<string> TransCodes { get; set; }
    
}

public class ReportWarning
{
    public string AgentCode { get; set; }

    public string AgentName { get; set; }

    public string CreatedDay { get; set; }
}

public class NotCompleteWarning
{
    public string AgentCode { get; set; }

    public string AgentName { get; set; }

    public string Content { get; set; }

    public bool Complete { get; set; }
    public bool ISend { get; set; }
}