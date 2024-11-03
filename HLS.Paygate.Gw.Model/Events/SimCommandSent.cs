namespace HLS.Paygate.Gw.Model.Events
{
    public interface SimCommandSent
    {
        string SimNumber { get; }
        string Command { get; }
    }
}