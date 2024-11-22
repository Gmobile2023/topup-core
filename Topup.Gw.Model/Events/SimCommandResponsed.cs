namespace HLS.Paygate.Gw.Model.Events
{
    public interface SimCommandResponsed
    {
        string SimNumber { get; }
        string Message { get; }
    }
}