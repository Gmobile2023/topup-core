using System;
using HLS.Paygate.Report.Model.Dtos;
using HLS.Paygate.Shared;
using MongoDbGenericRepository.Models;

namespace HLS.Paygate.Report.Domain.Entities;

//Gunner - Chỗ này xem lại các object. Đặt đúng tên entity. Để entity vào lớp domain. Phân biệt entity với Dto
public class ReportStaffDetail : Document
{
    #region 1.Thông tin loại giao dịch

    public string ServiceCode { get; set; }
    public string ServiceName { get; set; }

    #endregion

    #region 2.Thông tin về số tiền,mã giao dich, thời gian, trạng thái

    public string RequestRef { get; set; }
    public string TransCode { get; set; }
    public double Price { get; set; }
    public double DebitAmount { get; set; }
    public double CreditAmount { get; set; }
    public DateTime CreatedTime { get; set; }
    public string Description { get; set; }

    #endregion

    #region 3.Phần thanh toán +/- tiền tài khoản

    public double Balance { get; set; }
    public double LimitBalance { get; set; }

    public string TextDay { get; set; }
    public ReportStatus Status { get; set; }
    public bool IsView { get; set; }

    #endregion

    #region 4.Tài khoản thụ thưởng

    public string ReceivedCode { get; set; }    
    public string ReceivedInfo { get; set; }   

    #endregion

    #region 5.Thông tin cơ bản tài khoản sale

    public string AccountCode { get; set; }  
    public string AccountInfo { get; set; }

    #endregion

    #region 6.Thông tin cơ bản tài khoản salelead

    public string SaleLeaderCode { get; set; }

    public string SaleLeaderInfo { get; set; }    

    #endregion
}

public class ReportItemDetail : Document
{
    #region 1.Thông tin cơ bản tài khoản thực hiện giao dịch

    public string PerformAccount { get; set; }
    public string PerformInfo { get; set; }
    public int PerformAgentType { get; set; }

    #endregion

    #region 2.Thông tin loại giao dịch

    public string TransType { get; set; }
    public string ServiceCode { get; set; }
    public string ServiceName { get; set; }    

    #endregion

    #region 3.Thông tin về sản phẩm

    public string ProductCode { get; set; }
    public string ProductName { get; set; }
    public string CategoryCode { get; set; }
    public string CategoryName { get; set; }    

    #endregion

    #region 4.Thông tin về nhà cung cấp

    public string ProvidersCode { get; set; }
    public string ProvidersInfo { get; set; }    
    public string VenderCode { get; set; }
    public string VenderName { get; set; }

    #endregion

    #region 5.Thông tin về số tiền,mã giao dich, thời gian, trạng thái

    public string RequestRef { get; set; }
    public string TransCode { get; set; }
    public int Quantity { get; set; }
    public double Amount { get; set; }
    public double Discount { get; set; }
    public double Price { get; set; }
    public double TotalPrice { get; set; }
    public double Fee { get; set; }
    public double PriceIn { get; set; }
    public double PriceOut { get; set; }
    public DateTime CreatedTime { get; set; }    
    public string ParentCode { get; set; }
    public string ParentName { get; set; }
    public double? CommissionAmount { get; set; }    
    public int? CommissionStatus { get; set; }
    public DateTime? CommissionDate { get; set; }

    public string CommissionPaidCode { get; set; }

    #region 5.1.Phần thanh toán +/- tiền tài khoản

    public string PaidTransCode { get; set; }
    public double? PaidAmount { get; set; }  
    public string RequestTransSouce { get; set; }
    public string TransTransSouce { get; set; }
    public double? PerformBalance { get; set; }
    public double? Balance { get; set; }   
    public string FeeText { get; set; }

    #endregion

    #region 5.2.Phần Topup/Lấy mã thẻ

    public string PayTransRef { get; set; }  
    public string ReceivedAccount { get; set; }

    #endregion
    
    public ReportStatus Status { get; set; }
    public string TransNote { get; set; }    
    public string ExtraInfo { get; set; }
    public string Channel { get; set; }
    public string TextDay { get; set; }        

    #endregion

    #region 6.Thông tin cơ bản tài khoản nhận tiền(Dùng cho giao dịch chuyển tiền ngang)/Tài khoản đại lý

    public string AccountCode { get; set; }
    public string AccountInfo { get; set; }
    public int AccountAccountType { get; set; }
    public int AccountAgentType { get; set; }
    public string AccountAgentName { get; set; }
    public int AccountCityId { get; set; }
    public string AccountCityName { get; set; }
    public int AccountDistrictId { get; set; }
    public string AccountDistrictName { get; set; }
    public int AccountWardId { get; set; }
    public string AccountWardName { get; set; }
    public string SaleCode { get; set; }    
    public string SaleInfo { get; set; }
    public string SaleLeaderCode { get; set; }
    public string SaleLeaderInfo { get; set; }
    public string ReceiverType { get; set; }
    public string ProviderReceiverType { get; set; }
    public string ProviderTransCode { get; set; }
    public string ParentProvider { get; set; }
    
    #endregion
}