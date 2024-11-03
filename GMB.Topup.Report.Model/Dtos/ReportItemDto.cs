using System;
using HLS.Paygate.Gw.Model.Enums;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.Exceptions;
using MongoDbGenericRepository.Models;

namespace HLS.Paygate.Report.Model.Dtos
{
    public class ReportStaffDetail : Document
    {

        #region 1.Thông tin loại giao dịch        
        public string ServiceCode { get; set; }
        public ServiceItemDto ServiceItem { get; set; }

        #endregion

        #region 2.Thông tin về số tiền,mã giao dich, thời gian, trạng thái

        public string RequestRef { get; set; }
        public string TransCode { get; set; }
        public decimal Price { get; set; }
        public decimal DebitAmount { get; set; }
        public decimal CreditAmount
        {
            get; set;
        }
        public DateTime CreatedTime { get; set; }

        public string Description { get; set; }

        #endregion

        #region 3.Phần thanh toán +/- tiền tài khoản
        public decimal Balance { get; set; }
        public decimal LimitBalance { get; set; }

        public string TextDay { get; set; }
        public ReportStatus Status { get; set; }
        public bool IsView { get; set; }

        #endregion

        #region 4.Tài khoản thụ thưởng
        public string ReceivedCode { get; set; }
        public AccountItemDto ReceivedItem
        {
            get; set;
        }

        #endregion

        #region 5.Thông tin cơ bản tài khoản sale
        public string AccountCode { get; set; }

        public AccountItemDto AccountItem { get; set; }

        #endregion

        #region 6.Thông tin cơ bản tài khoản salelead
        public string SaleLeaderCode { get; set; }

        public AccountItemDto SaleLeaderItem { get; set; }

        #endregion

    }

    public class ReportItemDetailView: ReportItemDetail
    {
        public decimal PriceIn { get; set; }

        public decimal PriceOut { get; set; }        
    }
    public class ReportItemDetail : Document
    {
        #region 1.Thông tin cơ bản tài khoản thực hiện giao dịch
        public string PerformAccount { get; set; }

        public AccountItemDto PerformAccountItem { get; set; }

        #endregion

        #region 2.Thông tin loại giao dịch        
        public string ServiceCode { get; set; }
        public ServiceItemDto ServiceItem { get; set; }

        #endregion

        #region 3.Thông tin về sản phẩm      
        public string ProductCode { get; set; }
        public ProductItemDto ProductItem { get; set; }

        #endregion

        #region 4.Thông tin về nhà cung cấp       
        public string ProvidersCode { get; set; }
        public ProviderItemDto ProviderItem { get; set; }

        public string VenderCode { get; set; }

        public VenderItemDto VenderItem { get; set; }

        #endregion

        #region 5.Thông tin về số tiền,mã giao dich, thời gian, trạng thái

        public string RequestRef { get; set; }
        public string TransCode { get; set; }
        public int Quantity { get; set; }
        public decimal Amount { get; set; }
        public decimal Discount { get; set; }
        public decimal Price { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal Fee { get; set; }
        public DateTime CreatedTime { get; set; }

        public string TransType { get; set; }

        #region 5.1.Phần thanh toán +/- tiền tài khoản
        public string PaidTransCode { get; set; }
        public decimal? PaidAmount { get; set; }
        public int? PaidStatus { get; set; }
        public DateTime? PaidDate { get; set; }
        
        public string RequestTransSouce { get; set; }
        public string TransTransSouce { get; set; }

        public string PaidTransSouce { get; set; }
        public decimal? PerformBalance { get; set; }
        public decimal? Balance { get; set; }
        public bool? IsPayment { get; set; }

        #endregion

        #region 5.2.Phần Topup/Lấy mã thẻ
        public string PayTransRef { get; set; }
        public int? PayStatus { get; set; }
        public DateTime? PayDate { get; set; }
        public string ReceivedAccount { get; set; }

        #endregion

        public decimal AmountCost { get; set; }
        public ReportStatus Status { get; set; }
        public string TransNote { get; set; }
        public string Description { get; set; }
        public string ExtraInfo { get; set; }
        public string Channel { get; set; }
        public string TextDay { get; set; }
        public bool IsView { get; set; }
        public int NextStep { get; set; }

        #endregion

        #region 6.Thông tin cơ bản tài khoản nhận tiền(Dùng cho giao dịch chuyển tiền ngang)/Tài khoản đại lý

        public string AccountCode { get; set; }
        public AccountItemDto AccountItem { get; set; }
        public string SaleCode { get; set; }
        public AccountItemDto SaleItem { get; set; }
        public string SaleLeaderCode { get; set; }
        public AccountItemDto SaleLeaderItem { get; set; }

        #endregion

        public string FeeText { get; set; }

    }

    /// <summary>
    /// Thông tin object của dịch vụ
    /// </summary>
    public class ServiceItemDto
    {
        #region 2.Thông tin loại giao dịch
        public int? ServiceId { get; set; }
        public string ServiceCode { get; set; }
        public string ServicesName { get; set; }

        #endregion
    }

    /// <summary>
    /// Thông Tin Object của sản phẩm
    /// </summary>
    public class ProductItemDto
    {
        #region 3.Thông tin về sản phẩm
        public int? ProductId { get; set; }
        public string ProductCode { get; set; }
        public string ProductName { get; set; }
        public int? CategoryId { get; set; }
        public string CategoryCode { get; set; }
        public string CategoryName { get; set; }

        #endregion
    }

    /// <summary>
    /// Thông tin object của tài khoản
    /// </summary>
    public class AccountItemDto
    {
        public int UserId { get; set; }
        public string FullName { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public string Mobile { get; set; }
        public string ParentCode { get; set; }
        public string TreePath { get; set; }
        public int AccountType { get; set; }
        public int AgentType { get; set; }
        public string AgentName { get; set; }
        public string Gender { get; set; }
        public int NetworkLevel { get; set; }
        public int CityId { get; set; }
        public string CityName { get; set; }
        public int DistrictId { get; set; }
        public string DistrictName { get; set; }
        public int WardId { get; set; }
        public string WardName { get; set; }
        public string IdIdentity { get; set; }
    }

    /// <summary>
    /// Thông tin nhà cung cấp
    /// </summary>
    public class ProviderItemDto
    {
        #region 4.Thông tin về nhà cung cấp

        public int? ProvidersId { get; set; }
        public string ProvidersCode { get; set; }
        public string ProvidersName { get; set; }

        #endregion
    }

    /// <summary>
    /// Thông tin nhà cung cấp
    /// </summary>
    public class VenderItemDto
    {
        #region 4.Thông tin về nhà cung cấp

        public int? VenderId { get; set; }
        public string VenderCode { get; set; }
        public string VenderName { get; set; }

        #endregion
    }

    public class ProductFeeDto
    {
        public string ProductCode { get; set; }
        public string ProductName { get; set; }
        public decimal Amount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal FeeValue { get; set; }
        public int FeeId { get; set; }
        public int FeeDetailId { get; set; }

        public decimal? AmountMinFee { get; set; }

        public decimal? SubFee { get; set; }

        public decimal? AmountIncrease { get; set; }

        public decimal? MinFee { get; set; }
    }
}
