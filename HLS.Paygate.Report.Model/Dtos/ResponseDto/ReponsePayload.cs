using HLS.Paygate.Shared;

namespace HLS.Paygate.Report.Model.Dtos.ResponseDto;

public class ReponsePayload<T> : MessageResponseBase
{
    public new T Payload { get; set; }
    public int Total { get; set; }
}