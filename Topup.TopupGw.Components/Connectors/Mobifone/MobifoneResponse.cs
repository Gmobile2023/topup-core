using System.Collections.Generic;
using System.Runtime.Serialization;
using System;

namespace Topup.TopupGw.Components.Connectors.Mobifone;

public class MobifoneResponse
{
    [DataMember(Name = "fee")] public decimal Fee { get; set; }
    [DataMember(Name = "result")] public int Result { get; set; }
    [DataMember(Name = "result_namespace")] public string ResultNamespace { get; set; }
    [DataMember(Name = "schedule_id")] public long ScheduleId { get; set; }
    [DataMember(Name = "transid")] public int TransId { get; set; }
    [DataMember(Name = "avail")] public decimal Avail { get; set; }
    [DataMember(Name = "avail_1")] public decimal Avail1 { get; set; }
    [DataMember(Name = "avail_2")] public decimal Avail2 { get; set; }
    [DataMember(Name = "avail_3")] public decimal Avail3 { get; set; }
    [DataMember(Name = "current")] public decimal Current { get; set; }
    [DataMember(Name = "current_1")] public decimal Current1 { get; set; }
    [DataMember(Name = "current_2")] public decimal Current2 { get; set; }
    [DataMember(Name = "current_3")] public decimal Current3 { get; set; }
    [DataMember(Name = "pending")] public decimal Pending { get; set; }
    [DataMember(Name = "pending_1")] public decimal Pending1 { get; set; }
    [DataMember(Name = "pending_2")] public decimal Pending2 { get; set; }
    [DataMember(Name = "pending_3")] public decimal Pending3 { get; set; }
    [DataMember(Name = "detail")] public string Detail { get; set; }

    public ChecktransDetail ChecktransDetail { get; set; }
    public string GetResponseMessage(string responseCode)
    {
        Dictionary<string, string> listErrorCode = new Dictionary<string, string>();
        //listErrorCode.Add("1", "Yêu cầu không đúng định dạng");
        //listErrorCode.Add("2", "IP không được phép truy cập");
        //listErrorCode.Add("3", "Đối tác không tồn tại");
        //listErrorCode.Add("4", "Đối tác chưa login hoặc chưa thành công");
        //listErrorCode.Add("5", "Đối tác đã bị hủy");
        //listErrorCode.Add("6", "Đối tác đã bị khóa");
        //listErrorCode.Add("7", "Mật khẩu không đúng");
        //listErrorCode.Add("9", "Session hết hiệu lực");
        //listErrorCode.Add("10", "Session không hợp lệ");
        //listErrorCode.Add("11", "Không được truy cập vào thời điểm hiện tại");
        //listErrorCode.Add("12", "Không phải là đối tác SOAP. User chưa được phân quyền sử dụng chức năng SOAP");
        //listErrorCode.Add("13", "Vượt quá sesstion tối đa cho phép");
        //listErrorCode.Add("14", "Qúa số kết nối cho phép");
        //listErrorCode.Add("15", "Session chưa được tạo");
        //listErrorCode.Add("16", "Quá số user sử dụng loging trong một thời điểm");
        //listErrorCode.Add("18", "Sesion đã được login thành công và đang còn hiệu lực");
        //listErrorCode.Add("-2", "Không lấy được thông tin của user");
        //listErrorCode.Add("17", "Không đúng initiator");
        //listErrorCode.Add("19", "Không đủ tham số");
        //listErrorCode.Add("20", "Session chưa được login");
        //listErrorCode.Add("25", "Pin nhập không đúng 6 ký tự số");
        //listErrorCode.Add("29", "Kiểu tài khoản khác stock");
        //listErrorCode.Add("30", "Target khác postpaid và airtime");
        //listErrorCode.Add("31", "Số tiền không hợp lệ");

        listErrorCode.Add("0", "Giao dịch thành công");
        listErrorCode.Add("5", "Request Invalid Data");
        listErrorCode.Add("7", "Giao dịch thất bại. Lỗi ngoại lệ");
        listErrorCode.Add("1396", "PGS_INTERNAL_ERROR");
        listErrorCode.Add("1403", "PMI_PMI_NO_PM_CONNECTION");
        listErrorCode.Add("1404", "PMI_REQUEST_TIMEOUT");
        listErrorCode.Add("1501", "EZI_SERVICE_BUSY");
        listErrorCode.Add("1502", "EZI_SCLOGIC_RESPONSE_INVALID");
        listErrorCode.Add("1503", "EZI_NO_SCLOGIC_CONNECTION");
        listErrorCode.Add("1504", "EZI_REQUEST_TIMEOUT");
        listErrorCode.Add("1505", "EZI_BAD_DATA");
        listErrorCode.Add("1506", "EZI_BAD_TRANSID");
        listErrorCode.Add("1507", "EZI_BAD_MSISDN");
        listErrorCode.Add("1508", "EZI_BAD_SESSIONID");
        listErrorCode.Add("1509", "EZI_BAD_AMOUNT");
        listErrorCode.Add("1510", "EZI_BAD_LOGIN");
        listErrorCode.Add("1511", "EZI_BAD_AMOUNT_THAN_MORE_VCBALANCES");

        listErrorCode.Add("3500", "Partial Success");
        listErrorCode.Add("1001", "Manadatory Parameter Missing");
        listErrorCode.Add("1002", "Invalid Patameter Length");
        listErrorCode.Add("1003", "Invalid parameter Syntax");
        listErrorCode.Add("1004", "Other Error in Request");
        listErrorCode.Add("1071", "Amount not within Range");
        listErrorCode.Add("1072", "Invalid Date Time Format");
        listErrorCode.Add("1073", "Old and New Pin Same");
        listErrorCode.Add("1074", "Invalid Transaction Id");
        listErrorCode.Add("1075", "New and ConfirmNewPIN mismatch");

        listErrorCode.Add("3502", "PIN Modification Failure");
        listErrorCode.Add("3503", "Reseller Locked");
        listErrorCode.Add("3504", "Reseller Account Not Found");
        listErrorCode.Add("3505", "Insufficient Credit");
        listErrorCode.Add("3506", "Subscriber Busy");
        listErrorCode.Add("3507", "Destination Subscriber Busy");
        listErrorCode.Add("3508", "Error Limit Reached");
        listErrorCode.Add("3509", "Consecutive error limit reached");
        listErrorCode.Add("3510", "Wrong PIN");
        listErrorCode.Add("3511", "R2R to same account");
        listErrorCode.Add("3512", "Amount Less than min allowed");
        listErrorCode.Add("3513", "Amount higher than max allowed");
        listErrorCode.Add("3514", "Invalid Transaction");
        listErrorCode.Add("3515", "Originator validity Expired");
        listErrorCode.Add("3516", "Destination Validity Expired");
        listErrorCode.Add("3517", "Originator PIN not enabled");
        listErrorCode.Add("3518", "Commission table problem");
        listErrorCode.Add("3519", "Configuration Data problem");
        listErrorCode.Add("3520", "Unauthorized Transaction in profile");
        listErrorCode.Add("3521", "Invalid Level");

        listErrorCode.Add("5000", "Problem while receiving reply: Connection Timeout");
        listErrorCode.Add("501102", "Giao dịch đang chờ kết quả xử lý!");
        string value = "";
        listErrorCode.TryGetValue(responseCode, out value);

        return value ?? "";
    }
}

public class ChecktransDetail
{
    [DataMember(Name = "TRANS_TYPE")] public string TransType { get; set; }
    [DataMember(Name = "TRANS_DATE")] public DateTime TransDate { get; set; }
    [DataMember(Name = "DEST_MSISDN")] public string DestMSIDN { get; set; }
    [DataMember(Name = "TRANS_ID_RETURN")] public string TransId { get; set; }
    [DataMember(Name = "AMOUNT")] public decimal Amount { get; set; }
    [DataMember(Name = "RESULT")] public string Result { get; set; }

    public static string GetValue(string data, char separator = ':')
    {
        try
        {
            string value = "";
            var arr = data.Split(separator);
            if (arr.Length == 2)
                value = data.Split(separator)[1];
            if (arr.Length == 3)
                value = String.Format("{0}{1}", data.Split(separator)[1], data.Split(separator)[2]);
            if (arr.Length == 4)
                value = String.Format("{0}:{1}:{2}", data.Split(separator)[1], data.Split(separator)[2], data.Split(separator)[3]);

            return value;
        }
        catch (Exception ex)
        {
            return "";
        }
    }
}
