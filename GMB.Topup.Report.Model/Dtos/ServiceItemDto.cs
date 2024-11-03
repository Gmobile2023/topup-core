namespace GMB.Topup.Report.Model.Dtos;

/// <summary>
///     Thông tin object của dịch vụ
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
///     Thông Tin Object của sản phẩm
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
///     Thông tin object của tài khoản
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
///     Thông tin nhà cung cấp
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
///     Thông tin nhà cung cấp
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