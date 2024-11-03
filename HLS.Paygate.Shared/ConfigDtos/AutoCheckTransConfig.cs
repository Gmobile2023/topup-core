using System;

namespace HLS.Paygate.Shared.ConfigDtos
{
    public class BackendHangFireConfig
    {
        public bool IsRun { get; set; }
        public bool EnableHangfire { get; set; }
        public string ServerName { get; set; }
        public AutoCheckTransConfig AutoCheckTrans { get; set; }
        public CheckLastTransConfig CheckLastTrans { get; set; }
        public CheckAutoCloseProvider CheckAutoCloseProvider { get; set; }
    }

    public class AutoCheckTransConfig
    {
        public bool IsRun { get; set; }
        public bool IsSendTele { get; set; }
        public bool IsSendTeleSlowTrans { get; set; }
        public bool IsSendSuccess { get; set; }
        public bool IsProcess { get; set; }
        public bool IsSendTeleWarning { get; set; }
        public bool IsSendTeleWarningSlow { get; set; }
        public int TimePending { get; set; }
        public string CronExpression { get; set; }
        public int TimePendingWarning { get; set; }
        public int TimePendingWarningSlow { get; set; }
        public int MaxTransProcess { get; set; }

        /// <summary>
        /// Bù giao dịch lỗi nạp chậm
        /// </summary>
        public bool IsOffset { get; set; }

        public string PartnerCodeOffset { get; set; }
    }

    public class CheckLastTransConfig
    {
        public bool IsRun { get; set; }
        public string CronExpression { get; set; }
        public int CountResend { get; set; }
        public int TimeResend { get; set; }
    }

    public class CheckAutoCloseProvider
    {
        public bool IsRun { get; set; }
        public string CronExpression { get; set; }
       
    }

    public class CheckLastransInfo
    {
        public int TotalSend { get; set; }
        public DateTime LastTimeSend { get; set; }
    }
}