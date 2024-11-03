namespace HLS.Paygate.Gw.Model.Events
{
    public interface SimVinaProcessing : IEvent
    {
        string SimNumber { get; }   
    }
}