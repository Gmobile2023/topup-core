using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Topup.Report.Model.Dtos;
using Microsoft.Extensions.Logging;
using Topup.Report.Domain.Entities;

namespace Topup.Report.Domain.Exporting;

public partial class ExportDataExcel
{
    public bool ReportServiceDetailToFileCsv(string fileName, List<ReportServiceDetailDto> list)
    {
        try
        {
            using (var file = new StreamWriter(fileName, true, Encoding.UTF8))
            {
                var headerfiles =
                    "STT,Loại đại lý,Mã đại lý,NVKD,Nhà cung cấp,Dịch vụ,Loại sản phẩm,Tên sản phẩm,Đơn giá,Số lượng,Số tiền chiết khấu,Phí,Thành tiền,Hoa hồng ĐL tổng,Đại lý tổng,Số thụ hưởng,Thời gian,Trạng thái,Người thực hiện,Mã giao dịch,Mã đối tác,Mã nhà cung cấp,Kênh,Loại thuê bao,Mã NCC trả ra,Loại thuê bao NCC trả ra,Nhà cung cấp cha";
                file.WriteLine(headerfiles);
                var count = 1;
                foreach (var item in list)
                {
                    var tmp = string.Format(
                        "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20},{21},{22},{23},{24},{25},{26}",
                        count,
                        item.AgentTypeName,
                        item.AgentInfo,
                        item.StaffInfo,
                        item.VenderName,
                        item.ServiceName,
                        item.CategoryName,
                        item.ProductName,
                        item.Value,
                        item.Quantity,
                        item.Discount,
                        item.Fee,
                        item.Price,
                        item.CommistionAmount,
                        item.AgentParentInfo,
                        item.ReceivedAccount,
                        item.CreatedTime.ToString("dd/MM/yyyy HH:mm:ss"),
                        item.StatusName,
                        item.UserProcess,
                        item.TransCode,
                        item.RequestRef,
                        item.PayTransRef,
                        item.Channel,
                        item.ReceiverType,
                        item.ProviderTransCode,
                        item.ProviderReceiverType,
                        item.ParentProvider);
                    file.WriteLine(tmp);
                    count++;
                }

                _log.LogInformation(" Ghi Danh sach ReportServiceDetailToFileCsv thanh cong !");
                file.Close();
                return true;
            }
        }
        catch (Exception ex)
        {
            _log.LogError(
                $" Ghi Danh sach ReportServiceDetailToFileCsv Exception {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
            return false;
        }
    }

    public bool ReportTransDetailToFileCsv(string fileName, List<ReportTransDetailDto> lst)
    {
        try
        {
            using (var file = new StreamWriter(fileName, true, Encoding.UTF8))
            {
                var headerfiles =
                    "Trạng thái,Loại giao dịch,Nhà cung cấp,Đơn giá,Số lượng,Chiết khấu,Phí,Thu,Chi,Số dư,Tài khoản thụ hưởng,Mã giao dịch,Người thực hiện,Thời gian,Mã tham chiếu";
                file.WriteLine(headerfiles);
                var count = 1;
                foreach (var item in lst)
                {
                    var tmp = string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15}",
                        count,
                        item.StatusName,
                        item.TransTypeName,
                        item.Vender,
                        item.Amount,
                        item.Quantity,
                        item.Discount,
                        item.Fee,
                        item.PriceIn,
                        item.PriceOut,
                        item.Balance,
                        item.AccountRef,
                        item.TransCode,
                        item.UserProcess,
                        item.CreatedDate.ToString("dd/MM/yyyy HH:mm:ss"),
                        item.RequestTransSouce);
                    file.WriteLine(tmp);
                    count++;
                }

                _log.LogInformation(" Ghi Danh sach ReportTransDetailToFileCsv thanh cong !");
                file.Close();
                return true;
            }
        }
        catch (Exception ex)
        {
            _log.LogError(
                $" Ghi Danh sach ReportTransDetailToFileCsv Exception {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
            return false;
        }
    }

    public bool ReportDetailToFileCsv(string fileName, List<ReportDetailDto> lst)
    {
        try
        {
            using (var file = new StreamWriter(fileName, true, Encoding.UTF8))
            {
                var headerfiles =
                    "STT,Mã giao dịch,Loại giao dịch,Ngày giao dịch,Số dư trước giao dịch,Phát sinh tăng,Phát sinh giảm,Số dư sau giao dịch,Nội dung";
                file.WriteLine(headerfiles);
                var count = 1;
                foreach (var item in lst)
                {
                    var tmp = string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8}",
                        count,
                        item.TransCode,
                        item.ServiceName,
                        item.CreatedDate.ToString("dd/MM/yyyy HH:mm:ss"),
                        item.BalanceBefore,
                        item.Increment,
                        item.Decrement,
                        item.BalanceAfter,
                        item.TransNote);
                    file.WriteLine(tmp);
                    count++;
                }

                _log.LogInformation(" Ghi Danh sach ReportDetailToFileCsv thanh cong !");
                file.Close();
                return true;
            }
        }
        catch (Exception ex)
        {
            _log.LogError(
                $" Ghi Danh sach ReportDetailToFileCsv Exception {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
            return false;
        }
    }

    public bool ReportStaffDetailToFileCsv(string fileName, List<ReportStaffDetail> list)
    {
        try
        {
            using (var file = new StreamWriter(fileName, true, Encoding.UTF8))
            {
                var headerfiles =
                    "STT,Thời gian,Mã giao dịch,Loại công nợ,Nội dung,Số tiền phát sinh nợ,Số tiền thanh toán,Hạn mức còn lại";
                file.WriteLine(headerfiles);
                var count = 1;
                foreach (var item in list)
                {
                    var tmp = string.Format(
                        "{0},{1},{2},{3},{4},{5},{6},{7}",
                        count,
                        item.CreatedTime.ToString("dd/MM/yyyy HH:mm:ss"),
                        item.TransCode,
                        item.ServiceName,
                        item.Description,
                        item.DebitAmount,
                        item.CreditAmount,
                        item.Balance);
                    file.WriteLine(tmp);
                    count++;
                }

                _log.LogInformation(" Ghi Danh sach ReportStaffDetailToFileCsv thanh cong !");
                file.Close();
                return true;
            }
        }
        catch (Exception ex)
        {
            _log.LogError(
                $" Ghi Danh sach ReportStaffDetailToFileCsv Exception {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
            return false;
        }
    }

    public bool ReportTopupRequestLogToFileCsv(string fileName, List<ReportTopupRequestLogDto> list)
    {
        try
        {
            using (var file = new StreamWriter(fileName, true, Encoding.UTF8))
            {
                var headerfiles =
                    "STT,Mã giao dịch,Mã giao dịch đối tác,Dịch vụ,Loại sản phẩm,Mã sản phẩm,Nhà cung cấp,Mã đối tác,Thành tiền,Số thụ hưởng,Thời gian bắt đầu,Thời gian kết thúc,Trạng thái,Dữ liệu trả về từ NCC";
                file.WriteLine(headerfiles);
                var count = 1;
                foreach (var item in list)
                {
                    var tmp = string.Format(
                        "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13}",
                        count,
                        item.TransRef,
                        item.TransCode,
                        item.ServiceCode,
                        item.CategoryCode,
                        item.ProductCode,
                        item.ProviderCode,
                        item.PartnerCode,
                        item.TransAmount,
                        item.ReceiverInfo,
                        item.RequestDate.ToString("dd/MM/yyyy HH:mm:ss"),
                        item.ModifiedDate.ToString("dd/MM/yyyy HH:mm:ss"),
                        item.StatusName,
                        item.ResponseInfo);
                    file.WriteLine(tmp);
                    count++;
                }

                _log.LogInformation(" Ghi Danh sach ReportTopupRequestLogToFileCsv thanh cong !");
                file.Close();
                return true;
            }
        }
        catch (Exception ex)
        {
            _log.LogError(
                $" Ghi Danh sach ReportTopupRequestLogToFileCsv Exception {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
            return false;
        }
    }
}