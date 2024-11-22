namespace Topup.Contracts.Commands.Backend;

public interface CallBackCorrectTransCommand : ICommand
{
    string TransCode { get; set; }
    string ResponseCode { get; set; }
    string ResponseMessage { get; set; }
}