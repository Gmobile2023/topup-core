namespace HLS.Paygate.Gw.Model.Events
{
    public interface SimVinaInit : IEvent
    {
        string SimNumber { get; }   
    }
}