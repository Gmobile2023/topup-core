using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Topup.Report.Model.Dtos;
using Topup.Report.Model.Dtos.RequestDto;
using Topup.Shared;
using Microsoft.Extensions.Logging;
using ServiceStack;
using Topup.Report.Domain.Entities;

namespace Topup.Report.Domain.Services;

public partial class BalanceReportService
{
    public async Task ExportFileDataAgent(DateTime date, List<ReportItemDetail> saleItems, List<ReportAgentBalanceDto> balanceItems, ReportFile sourcePath)
    {
        try
        {
            if (saleItems == null)
                saleItems = await ReportQueryItemRequest(date, string.Empty);
            if (balanceItems == null)
            {
                var listAgents = await ReportAgentBalanceGetList(new ReportAgentBalanceRequest
                {
                    FromDate = date,
                    ToDate = date,
                    Limit = int.MaxValue,
                    Offset = 0
                });

                balanceItems = listAgents.Payload.ConvertTo<List<ReportAgentBalanceDto>>();
            }


            var litDeposit = saleItems.Where(c => c.TransType == ReportServiceCode.DEPOSIT).ToList();
            var litTransfer = saleItems.Where(c => c.TransType == ReportServiceCode.TRANSFER).ToList();
            var litSale = saleItems.Where(c => c.ServiceCode == ReportServiceCode.PIN_CODE
                                          || c.ServiceCode == ReportServiceCode.PIN_DATA
                                          || c.ServiceCode == ReportServiceCode.PIN_GAME
                                          || c.ServiceCode == ReportServiceCode.TOPUP
                                          || c.ServiceCode == ReportServiceCode.TOPUP_DATA
                                          || c.ServiceCode == ReportServiceCode.PAY_BILL).ToList();

            await ExportFileTrans(saleItems, sourcePath, date);
            await ExportFileTransDeposit(litDeposit, sourcePath, date);
            await ExportFileTransfer(litTransfer, sourcePath, date);
            await ExportFileTransSale(litSale, sourcePath, date);
            await ExportFileBalanceAgent(balanceItems, sourcePath, date);
        }
        catch (Exception ex)
        {
            _logger.LogError($"ExportFileDataAgent: Exception {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
        }
    }

    public async Task ExportFileSaleByPartner(List<ReportServiceDetailDto> list, string sourcesourcePath)
    {
        try
        {           
            IsWriteFileSalePartnerCsv(sourcesourcePath, list);
        }
        catch (Exception ex)
        {
            _logger.LogError($"ExportFile ExportFileTransfer Exception: {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
        }
    }

    public async Task ExportFileBalanceHistory(DateTime date, List<ReportBalanceHistories> list, ReportFile sourcePath)
    {
        try
        {
            if (list == null)
                list = await ReportQueryHistoryRequest(date, string.Empty);
            var headder = "STT,AccountCode,BalanceBefore,AmountUp,AmountDown,BalanceAfter,CreatedDate,ServiceCode,ServiceName,TransCode,TransNote,Description";
            var fileName = $"Report.BalanceHistory.{date:yyyyMMdd}.csv";
            var pathSave = $"{sourcePath.PathName}/{fileName}";

            IsWriteFileBalanceHistoryCsv(headder, pathSave, list);
        }
        catch (Exception ex)
        {
            _logger.LogError($"ExportFile Transaction Exception: {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
        }
    }

    private bool IsWriteFileTransCsv(string headerfiles,
        string fileName, List<ReportItemDetail> list)
    {
        try
        {
            using (var file = new StreamWriter(fileName, true, Encoding.UTF8))
            {
                _logger.LogInformation($"{fileName} Ghi headerfiles");
                var sb = new StringBuilder();
                sb.Append(headerfiles);
                file.WriteLine(sb.ToString());
                var count = 1;
                _logger.LogInformation($"{fileName} TotalRow : {list.Count()}");
                foreach (var item in list.OrderBy(c => c.CreatedTime))
                {
                    try
                    {
                        sb = new StringBuilder();
                        sb.Append(count).Append(","); //STT
                        sb.Append(item.PerformAccount ?? string.Empty).Append(",");
                        sb.Append(item.AccountCode).Append(","); //AgentCode
                        sb.Append(item.AccountInfo).Append(","); //AgentInfo
                        sb.Append(item.AccountAgentType).Append(","); //AgentType
                        sb.Append(item.AccountAgentName).Append(","); //AgentName                                                                          
                        sb.Append(item.ServiceCode).Append(","); //ServiceCode
                        sb.Append(item.ServiceName).Append(","); //ServiceName
                        sb.Append(item.TransType).Append(","); //TransType
                        sb.Append(item.ProductCode ?? string.Empty).Append(","); //ProductCode
                        sb.Append(item.ProductName).Append(","); //ProductName
                        sb.Append(item.CategoryCode).Append(","); //CategoryCode
                        sb.Append(item.CategoryName).Append(","); //CategoryName
                        sb.Append(_dateHepper.ConvertToUserTime(item.CreatedTime, DateTimeKind.Utc).ToString("yyyy-MM-dd HH:mm:ss")).Append(","); //CreateDate
                        sb.Append(item.RequestRef ?? string.Empty).Append(","); //RequestRef
                        sb.Append(item.TransCode ?? string.Empty).Append(","); //TransCode
                        sb.Append(item.PaidTransCode ?? string.Empty).Append(","); //PaidTransCode
                        sb.Append(item.PayTransRef ?? string.Empty).Append(","); //PayTransRef
                        sb.Append(item.Status).Append(","); //Status
                        sb.Append(item.Quantity).Append(","); //Quantity
                        sb.Append(item.Amount).Append(","); //Amount
                        sb.Append(item.Fee).Append(","); //Fee
                        sb.Append(item.Discount).Append(","); //Discount
                        sb.Append(item.TotalPrice).Append(","); //Price
                        sb.Append(item.Balance ?? 0).Append(","); //Balance
                        sb.Append(item.PerformBalance ?? 0).Append(","); //ProcessBalance
                        sb.Append(item.ReceivedAccount ?? string.Empty).Append(","); //ReceivedAccount
                        sb.Append(item.Channel ?? string.Empty).Append(","); //Channel
                        sb.Append(item.ProvidersCode ?? string.Empty).Append(","); //ProviderCode
                        sb.Append(item.SaleCode ?? string.Empty).Append(","); //SaleCode
                        sb.Append(item.SaleInfo ?? string.Empty).Append(","); //SaleCodeInfo                        
                        sb.Append(item.SaleLeaderCode ?? string.Empty).Append(","); //LeaderCode
                        sb.Append(item.SaleLeaderInfo ?? string.Empty).Append(","); //LeaderInfo
                        sb.Append(item.AccountCityId).Append(","); //CityId
                        sb.Append(item.AccountCityName).Append(","); //CityName
                        sb.Append(item.AccountDistrictId).Append(","); //DistrictId
                        sb.Append(item.AccountDistrictName).Append(","); //DistrictName
                        sb.Append(item.AccountWardId).Append(","); //WardId
                        sb.Append(item.AccountWardName).Append(","); //WardName
                        sb.Append(item.CommissionAmount).Append(","); //CommissionAmount
                        sb.Append(item.CommissionStatus).Append(","); //CommissionStatus
                        sb.Append(item.CommissionPaidCode).Append(","); //CommissionPaidCode,
                        sb.Append(item.CommissionDate != null ? _dateHepper.ConvertToUserTime(item.CommissionDate.Value, DateTimeKind.Utc).ToString("yyyy-MM-dd HH:mm:ss") : "").Append(",");//CommissionDate
                        sb.Append(item.ParentCode).Append(",");//ParentCode,
                        sb.Append(item.ParentName);//ParentName,
                        file.WriteLine(sb.ToString());
                    }
                    catch (Exception exItem)
                    {
                        _logger.LogInformation($"{item.AccountCode} - {item.RequestRef} - {item.PaidTransCode} =>{item.ToJson()}");
                        _logger.LogInformation($"{item.PaidTransCode} Exception: {exItem.Message}|{exItem.InnerException}|{exItem.StackTrace}");
                    }
                    count++;
                }

                _logger.LogInformation($"{fileName} Map xong !");
                _logger.LogInformation($"{fileName} Ghi Danh sach thanh cong !");
                file.Close();

                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"{fileName} Ghi Danh sach Exception {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
            return false;
        }
    }

    private bool IsWriteFileTransferCsv(string headerfiles,
        string fileName, List<ReportItemDetail> list)
    {
        try
        {
            using (var file = new StreamWriter(fileName, true, Encoding.UTF8))
            {
                _logger.LogInformation($"{fileName} Ghi headerfiles");
                var sb = new StringBuilder();
                sb.Append(headerfiles);
                file.WriteLine(sb.ToString());
                var count = 1;
                _logger.LogInformation($"{fileName} TotalRow : {list.Count()}");
                foreach (var item in list.OrderBy(c => c.CreatedTime))
                {
                    try
                    {
                        sb = new StringBuilder();
                        sb.Append(count).Append(","); //STT
                        sb.Append(item.PerformAccount ?? string.Empty).Append(","); //ProcessCode
                        sb.Append(item.PerformInfo ?? string.Empty).Append(","); //ProcessInfo
                        sb.Append(item.AccountCode).Append(","); //AgentCode
                        sb.Append(item.AccountInfo).Append(","); //AgentInfoInfo
                        sb.Append(item.AccountAgentType).Append(",");//AgentType                      
                        sb.Append(item.AccountAgentName).Append(","); //AgentName                        
                        sb.Append(item.ServiceCode).Append(","); //ServiceCode
                        sb.Append(item.ServiceName).Append(","); //ServiceName                                             
                        sb.Append(_dateHepper.ConvertToUserTime(item.CreatedTime, DateTimeKind.Utc).ToString("yyyy-MM-dd HH:mm:ss")).Append(",");//CreateDate
                        sb.Append(item.TransCode ?? string.Empty).Append(","); //TransCode
                        sb.Append(item.PaidTransCode ?? string.Empty).Append(",");//PaidTransCode
                        sb.Append(item.Status).Append(","); //Status
                        sb.Append(item.Amount).Append(","); //Amount
                        sb.Append(item.TotalPrice).Append(","); //Price
                        sb.Append(item.Balance ?? 0).Append(","); //Balance
                        sb.Append(item.PerformBalance ?? 0).Append(",");//ProcessBalance
                        sb.Append(item.Channel ?? string.Empty).Append(",");//Channel
                        sb.Append(item.SaleCode ?? string.Empty).Append(",");//SaleCode
                        sb.Append(item.SaleInfo ?? string.Empty).Append(",");//SaleCode                        
                        sb.Append(item.SaleLeaderCode ?? string.Empty).Append(","); //LeaderCode
                        sb.Append(item.SaleLeaderInfo ?? string.Empty).Append(","); //LeaderInfo
                        sb.Append(item.AccountCityId).Append(","); //CityId
                        sb.Append(item.AccountCityName).Append(","); //CityName
                        sb.Append(item.AccountDistrictId).Append(","); //DistrictId
                        sb.Append(item.AccountDistrictName).Append(","); //DistrictName
                        sb.Append(item.AccountWardId).Append(","); //WardId
                        sb.Append(item.AccountWardName).Append(","); //WardName

                        file.WriteLine(sb.ToString());
                    }
                    catch (Exception exItem)
                    {
                        _logger.LogInformation(
                            $"{item.AccountCode} - {item.RequestRef} - {item.PaidTransCode} =>{item.ToJson()}");
                        _logger.LogInformation(
                            $"{item.PaidTransCode} Exception: {exItem.Message}|{exItem.InnerException}|{exItem.StackTrace}");
                    }

                    count++;
                }

                _logger.LogInformation($"{fileName} Map xong !");
                _logger.LogInformation($"{fileName} Ghi Danh sach thanh cong !");
                file.Close();

                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"{fileName} Ghi Danh sach Exception {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
            return false;
        }
    }

    private bool IsWriteFileTransDepositCsv(string headerfiles,
        string fileName, List<ReportItemDetail> list)
    {
        try
        {
            using (var file = new StreamWriter(fileName, true, Encoding.UTF8))
            {
                _logger.LogInformation($"{fileName} Ghi headerfiles");
                var sb = new StringBuilder();
                sb.Append(headerfiles);
                file.WriteLine(sb.ToString());
                var count = 1;
                _logger.LogInformation($"{fileName} TotalRow : {list.Count()}");
                foreach (var item in list.OrderBy(c => c.CreatedTime))
                {
                    try
                    {
                        sb = new StringBuilder();
                        sb.Append(count).Append(","); //STT
                        sb.Append(item.AccountCode).Append(",");//AgentCode
                        sb.Append(item.AccountInfo).Append(",");//AccountInfo
                        sb.Append(item.AccountAgentType).Append(",");//AgentType
                        sb.Append(item.AccountAgentName).Append(",");//AgentName
                        sb.Append(item.ServiceCode).Append(",");//ServiceCode
                        sb.Append(item.ServiceName).Append(",");//ServiceName
                        sb.Append(_dateHepper.ConvertToUserTime(item.CreatedTime, DateTimeKind.Utc).ToString("yyyy-MM-dd HH:mm:ss")).Append(",");//CreateDate
                        sb.Append(item.TransCode ?? string.Empty).Append(","); //TransCode
                        sb.Append(item.PaidTransCode ?? string.Empty).Append(",");//PaidTransCod
                        sb.Append(item.Status).Append(","); //Status
                        sb.Append(item.Amount).Append(","); //Amount
                        sb.Append(item.TotalPrice).Append(","); //Price
                        sb.Append(item.Balance ?? 0).Append(","); //Balance
                        sb.Append(item.Channel ?? string.Empty).Append(","); //Channel
                        sb.Append(item.SaleCode ?? string.Empty).Append(","); //SaleCode
                        sb.Append(item.SaleInfo ?? string.Empty).Append(","); //SaleInfo                        
                        sb.Append(item.SaleLeaderCode ?? string.Empty).Append(","); //LeaderCode
                        sb.Append(item.SaleLeaderInfo ?? string.Empty).Append(","); //LeaderInfo
                        sb.Append(item.AccountCityId).Append(","); //CityId
                        sb.Append(item.AccountCityName).Append(","); //CityName
                        sb.Append(item.AccountDistrictId).Append(","); //DistrictId
                        sb.Append(item.AccountDistrictName).Append(","); //DistrictName
                        sb.Append(item.AccountWardId).Append(","); //WardId
                        sb.Append(item.AccountWardName).Append(","); //WardName
                        file.WriteLine(sb.ToString());
                    }
                    catch (Exception exItem)
                    {
                        _logger.LogInformation(
                            $"{item.AccountCode} - {item.RequestRef} - {item.PaidTransCode} =>{item.ToJson()}");
                        _logger.LogInformation(
                            $"{item.PaidTransCode} Exception: {exItem.Message}|{exItem.InnerException}|{exItem.StackTrace}");
                    }

                    count++;
                }

                _logger.LogInformation($"{fileName} Map xong !");
                _logger.LogInformation($"{fileName} Ghi Danh sach thanh cong !");
                file.Close();

                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"{fileName} Ghi Danh sach Exception {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
            return false;
        }
    }

    private bool IsWriteFileBalaneAgentCsv(string headerfiles,
        string fileName, List<ReportAgentBalanceDto> list)
    {
        try
        {
            using (var file = new StreamWriter(fileName, true, Encoding.UTF8))
            {
                _logger.LogInformation($"{fileName} Ghi headerfiles");
                var sb = new StringBuilder();
                sb.Append(headerfiles);
                file.WriteLine(sb.ToString());
                var count = 1;
                _logger.LogInformation($"{fileName} TotalRow : {list.Count()}");
                foreach (var item in list.OrderBy(c => c.AgentCode))
                {
                    try
                    {
                        sb = new StringBuilder();
                        sb.Append(count).Append(","); //STT
                        sb.Append(item.AgentCode).Append(","); //AgentCode
                        sb.Append(item.AgentInfo).Append(","); //AgentInfo                          
                        sb.Append(item.BeforeAmount).Append(","); //BalanceBefore
                        sb.Append(item.AmountUp).Append(","); //tien vao
                        sb.Append(item.AmountDown).Append(","); //tien ra
                        sb.Append(item.AfterAmount).Append(","); //BalanceAfter
                        sb.Append(item.SaleCode ?? string.Empty).Append(","); //SaleCode
                        sb.Append(item.SaleInfo).Append(","); //SaleInfo:UserName                          
                        sb.Append(item.SaleLeaderCode ?? string.Empty).Append(","); //LeaderCode
                        sb.Append(item.SaleLeaderInfo).Append(","); //LeaderInfo:UserName                           
                        file.WriteLine(sb.ToString());
                    }
                    catch (Exception exItem)
                    {
                        _logger.LogInformation($"{item.AgentCode}  =>{item.ToJson()}");
                        _logger.LogInformation(
                            $"{item.AgentCode} Exception: {exItem.Message}|{exItem.InnerException}|{exItem.StackTrace}");
                    }

                    count++;
                }

                _logger.LogInformation($"{fileName} Map xong !");
                _logger.LogInformation($"{fileName} Ghi Danh sach thanh cong !");
                file.Close();

                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"{fileName} Ghi Danh sach Exception {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
            return false;
        }
    }

    private bool IsWriteFileBalanceHistoryCsv(string headerfiles,
        string fileName, List<ReportBalanceHistories> list)
    {
        try
        {
            using (var file = new StreamWriter(fileName, true, Encoding.UTF8))
            {
                _logger.LogInformation($"{fileName} Ghi headerfiles");
                var sb = new StringBuilder();
                sb.Append(headerfiles);
                file.WriteLine(sb.ToString());
                var count = 1;
                _logger.LogInformation($"{fileName} TotalRow : {list.Count()}");
                foreach (var item in list.OrderBy(c => c.CreatedDate))
                {
                    try
                    {
                        //STT,AccountCode,BalanceBefore,AmountUp,AmountDown,BalanceAfter,CreatedDate,ServiceCode,ServiceName,TransCode,TransNote,Description
                        sb = new StringBuilder();
                        sb.Append(count).Append(",");
                        sb.Append(item.SrcAccountCode).Append(",");
                        sb.Append(item.SrcAccountBalanceBeforeTrans).Append(",");
                        sb.Append("0").Append(",");
                        sb.Append(item.Amount).Append(",");
                        sb.Append(item.SrcAccountBalanceAfterTrans).Append(",");
                        sb.Append(_dateHepper.ConvertToUserTime(item.CreatedDate, DateTimeKind.Utc)
                            .ToString("yyyy-MM-dd HH:mm:ss")).Append(",");
                        sb.Append(item.ServiceCode).Append(",");
                        sb.Append(item.ServiceName).Append(",");
                        sb.Append(item.TransCode).Append(",");
                        sb.Append(item.TransNote).Append(",");
                        sb.Append(item.Description);
                        file.WriteLine(sb.ToString());
                        sb = new StringBuilder();
                        sb.Append(count).Append(",");
                        sb.Append(item.DesAccountCode).Append(",");
                        sb.Append(item.DesAccountBalanceBeforeTrans).Append(",");
                        sb.Append(item.Amount).Append(",");
                        sb.Append("0").Append(",");
                        sb.Append(item.DesAccountBalanceAfterTrans).Append(",");
                        sb.Append(_dateHepper.ConvertToUserTime(item.CreatedDate, DateTimeKind.Utc)
                            .ToString("yyyy-MM-dd HH:mm:ss")).Append(",");
                        sb.Append(item.ServiceCode).Append(",");
                        sb.Append(item.ServiceName).Append(",");
                        sb.Append(item.TransCode).Append(",");
                        sb.Append(item.TransNote).Append(",");
                        sb.Append(item.Description);
                        file.WriteLine(sb.ToString());
                    }
                    catch (Exception exItem)
                    {
                        _logger.LogInformation(
                            $"{item.TransCode} Exception: {exItem.Message}|{exItem.InnerException}|{exItem.StackTrace}");
                    }

                    count++;
                }

                _logger.LogInformation($"{fileName} Map xong !");
                _logger.LogInformation($"{fileName} Ghi Danh sach thanh cong !");
                file.Close();

                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"{fileName} Ghi Danh sach Exception {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
            return false;
        }
    }
    private bool IsWriteFileSalePartnerCsv(string fileName, List<ReportServiceDetailDto> list)
    {
        try
        {
            var headerfiles = "STT,Loại đại lý,Mã đại lý,Dịch vụ,Loại sản phẩm,Tên sản phẩm,Đơn giá,Số lượng,Chiết khấu,Phí,Thành tiền,Số thụ hưởng,Thời gian,Trạng thái,Người thực hiện,Mã giao dịch,Kênh,Loại thuê bao,Ghi chú";
            using (var file = new StreamWriter(fileName, true, Encoding.UTF8))
            {
                _logger.LogInformation($"{fileName} Ghi headerfiles");
                var sb = new StringBuilder();
                sb.Append(headerfiles);
                file.WriteLine(sb.ToString());
                var count = 1;
                _logger.LogInformation($"{fileName} TotalRow : {list.Count()}");
                foreach (var item in list.OrderBy(c => c.CreatedTime))
                {
                    try
                    {
                        sb = new StringBuilder();
                        sb.Append(count).Append(","); //STT
                        sb.Append(item.AgentTypeName ?? string.Empty).Append(",");
                        sb.Append(item.AgentInfo).Append(",");
                        sb.Append(item.ServiceName).Append(",");
                        sb.Append(item.CategoryName).Append(",");
                        sb.Append(item.ProductName).Append(",");
                        sb.Append(item.Value).Append(",");
                        sb.Append(item.Quantity).Append(",");
                        sb.Append(item.Discount).Append(",");
                        sb.Append(item.Fee).Append(",");
                        sb.Append(item.Price).Append(",");
                        sb.Append(item.ReceivedAccount).Append(",");
                        sb.Append(item.CreatedTime).Append(",");
                        sb.Append(item.StatusName).Append(",");
                        sb.Append(item.UserProcess).Append(",");
                        sb.Append(item.RequestRef).Append(",");
                        sb.Append(item.Channel).Append(",");
                        sb.Append(item.ReceiverType).Append(",");                     
                        file.WriteLine(sb.ToString());
                    }
                    catch (Exception exItem)
                    {
                        _logger.LogInformation($"{item.TransCode} - {item.RequestRef} =>{item.ToJson()}");
                        _logger.LogInformation($"{item.TransCode} Exception: {exItem.Message}|{exItem.InnerException}|{exItem.StackTrace}");
                    }

                    count++;
                }

                _logger.LogInformation($"{fileName} Map xong !");
                _logger.LogInformation($"{fileName} Ghi Danh sach thanh cong !");
                file.Close();

                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"{fileName} Ghi Danh sach Exception {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
            return false;
        }
    }
    private async Task ExportFileTrans(List<ReportItemDetail> list, ReportFile sourcePath, DateTime date)
    {
        try
        {
            var headder =
             "STT,ProcessCode,AgentCode,AgentInfo,AgentType,AgentName,ServiceCode,ServiceName,TransType,ProductCode,ProductName,CategoryCode,CategoryName,CreateDate,RequestRef,TransCode,PaidTransCode,PayTransRef,";
            headder +=
                "Status,Quantity,Amount,Fee,Discount,Price,Balance,ProcessBalance,ReceivedAccount,Channel,ProviderCode,SaleCode,SaleInfo,LeaderCode,LeaderInfo,";
            headder += "Agent_CityID,Agent_CityName,Agent_DistrictID,Agent_DistrictName,Agent_WardID,Agent_WardName,";
            headder += "CommissionAmount,CommissionStatus,CommissionPaidCode,CommissionDate,ParentCode,ParentName";
            var lis = list.Where(c => !string.IsNullOrEmpty(c.PaidTransCode)).ToList();
            var fileName = $"Report.Trans.{date:yyyyMMdd}.csv";
            var pathSave = $"{sourcePath.PathName}/{fileName}";

            IsWriteFileTransCsv(headder, pathSave, lis);
        }
        catch (Exception ex)
        {
            _logger.LogError($"ExportFile Transaction Exception: {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
        }
    }
    private async Task ExportFileTransSale(List<ReportItemDetail> list, ReportFile sourcePath, DateTime date)
    {
        try
        {
            var headder =
              "STT,ProcessCode,AgentCode,AgentInfo,AgentType,AgentName,ServiceCode,ServiceName,TransType,ProductCode,ProductName,CategoryCode,CategoryName,CreateDate,RequestRef,TransCode,PaidTransCode,PayTransRef,";
            headder +=
                "Status,Quantity,Amount,Fee,Discount,Price,Balance,ProcessBalance,ReceivedAccount,Channel,ProviderCode,SaleCode,SaleInfo,LeaderCode,LeaderInfo,";
            headder += "Agent_CityID,Agent_CityName,Agent_DistrictID,Agent_DistrictName,Agent_WardID,Agent_WardName,";
            headder += "CommissionAmount,CommissionStatus,CommissionPaidCode,CommissionDate,ParentCode,ParentName";
            var lis = list.Where(c => !string.IsNullOrEmpty(c.PaidTransCode)).ToList();
            var fileName = $"Report.TransSale.{date:yyyyMMdd}.csv";
            var pathSave = $"{sourcePath.PathName}/{fileName}";

            IsWriteFileTransCsv(headder, pathSave, lis);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                $"ExportFile ExportFileTransSale Exception: {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
        }
    }
    private async Task ExportFileTransDeposit(List<ReportItemDetail> list, ReportFile sourcePath, DateTime date)
    {
        try
        {
            var headder = "STT,AgentCode,AgentInfo,AgentType,AgentName,ServiceCode,ServiceName,CreateDate,TransCode,PaidTransCode,";
            headder += "Status,Amount,Price,Balance,Channel,SaleCode,SaleInfo,LeaderCode,LeaderInfo,";
            headder += "Agent_CityID,Agent_CityName,Agent_DistrictID,Agent_DistrictName,Agent_WardID,Agent_WardName";
            var lis = list.Where(c => !string.IsNullOrEmpty(c.PaidTransCode)).ToList();
            var fileName = $"Report.TransDeposit.{date:yyyyMMdd}.csv";
            var pathSave = $"{sourcePath.PathName}/{fileName}";
            IsWriteFileTransDepositCsv(headder, pathSave, lis);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                $"ExportFile ExportFileTransDeposit Exception: {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
        }
    }
    private async Task ExportFileBalanceAgent(List<ReportAgentBalanceDto> list, ReportFile sourcePath, DateTime date)
    {
        try
        {
            var headder = "STT,AgentCode,AgentName,BalanceBefore,AmountIn,AmountOut,BalanceAfter,";
            headder += "SaleCode,SaleFullName,LeaderCode,LeaderFullName";
            var fileName = $"Report.BalanceAgent.{date.ToString("yyyyMMdd")}.csv";
            var pathSave = $"{sourcePath.PathName}/{fileName}";

            IsWriteFileBalaneAgentCsv(headder, pathSave, list);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                $"ExportFile ExportFileTransDeposit Exception: {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
        }
    }
    private async Task ExportFileTransfer(List<ReportItemDetail> list, ReportFile sourcePath, DateTime date)
    {
        try
        {
            var headder =
                "STT,ProcessCode,ProcessMobile,ProcessName,ProcessType,AgentCode,AgentMobile,AgentName,AgentType,AgentName,ServiceCode,ServiceName,TransType,TransCode,PaidTransCode";
            headder +=
                "Status,Amount,Price,Balance,ProcessBalance,Channel,ProviderCode,SaleCode,SaleUser,SaleMobile,SaleName,LeaderCode,LeaderUser,LeaderMobile,LeaderName,";
            headder +=
                "Process_CityID,Process_CityName,Process_DistrictID,Process_DistrictName,Process_WardID,Process_WardName,";
            headder += "Agent_CityID,Agent_CityName,Agent_DistrictID,Agent_DistrictName,Agent_WardID,Agent_WardName,";
            var lis = list.Where(c => !string.IsNullOrEmpty(c.PaidTransCode)).ToList();
            var fileName = $"Report.Transfer.{date:yyyyMMdd}.csv";
            var pathSave = $"{sourcePath.PathName}/{fileName}";
            IsWriteFileTransferCsv(headder, pathSave, lis);
        }
        catch (Exception ex)
        {
            _logger.LogError($"ExportFile ExportFileTransfer Exception: {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
        }
    }

    public  async Task<ReportFile> GetForderCreate(string key, string extension = "")
    {
        try
        {
            var dto = new ReportFile
            {
                Folder = DateTime.Now.ToString("yyMMdd_HHmmss"),
            };
            var sourcePath = Path.Combine("", $"ReportFiles/{key}/{dto.Folder}");
            if (!Directory.Exists(sourcePath))
                Directory.CreateDirectory(sourcePath);

            dto.PathName = sourcePath;
            dto.FileZip = dto.Folder + (string.IsNullOrEmpty(extension) ? ".rar" : extension);
            dto.KeySouce = key;
            return dto;
        }
        catch (Exception ex)
        {
            _logger.LogError($"key={key} getForderCreate Exception: {ex.Message}|{ex.InnerException}|{ex.InnerException}");
            return null;
        }
    }

    public async Task<string> ZipForderCreate(ReportFile sourceFile)
    {
        try
        {
            var desPath = Path.Combine("", $"ReportFiles/{ReportConst.ZIP}/{sourceFile.KeySouce}");
            if (!Directory.Exists(desPath))
                Directory.CreateDirectory(desPath);
            ZipFile.CreateFromDirectory($"{sourceFile.PathName}", $"{desPath}/{sourceFile.FileZip}");
            await DeleteFileNow(string.Empty, sourceFile.PathName);
            return desPath + "/" + sourceFile.FileZip;
        }
        catch (Exception ex)
        {
            _logger.LogError($"ZipForderCreate Exception: {ex.Message}|{ex.InnerException}|{ex.InnerException}");
            return string.Empty;
        }
    }

    public async Task DeleteFileNow(string key, string sourceFile)
    {
        try
        {
            if (!string.IsNullOrEmpty(key))
            {
                var sourcePath = Path.Combine("", $"ReportFiles/{key}");
                if (Directory.Exists(sourcePath))
                {

                    foreach (var note in Directory.GetDirectories(sourcePath))
                    {
                        foreach (var note2 in Directory.GetDirectories(note))
                        {
                            if (Directory.Exists(note2))
                            {
                                await DeleteFileInt(note2);
                                Directory.Delete(note2);
                            }
                        }

                        if (Directory.Exists(note))
                        {
                            await DeleteFileInt(note);
                            Directory.Delete(note);
                        }
                    }

                    await DeleteFileInt(sourcePath);
                    Directory.Delete(sourcePath);
                }
            }

            if (!string.IsNullOrEmpty(sourceFile))
            {
                if (Directory.Exists(sourceFile))
                {
                    await DeleteFileInt(sourceFile);
                    Directory.Delete(sourceFile);
                }
            }

        }
        catch (Exception ex)
        {
            _logger.LogError($"DeleteFileNow Exception: {ex.Message}|{ex.InnerException}|{ex.InnerException}");
        }
    }

    private async Task DeleteFileInt(string sourceFile)
    {
        try
        {
            foreach (var f in Directory.GetFiles(sourceFile))
            {
                if (File.Exists(f))
                    File.Delete(f);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"DeleteFileInt Exception: {ex.Message}|{ex.InnerException}|{ex.InnerException}");
        }
    }
}