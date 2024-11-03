using System;
using Orleans;
using ServiceStack;
using ServiceStack.Model;

namespace HLS.Paygate.Balance.Models.Exceptions;

[GenerateSerializer]
public class BalanceException : Exception, IResponseStatusConvertible
{
    // public string Message { get; set; }
    public BalanceException(int errorCode)
    {
        ErrorCode = errorCode;
    }

    public BalanceException(int errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
    }

    [Id(0)]
    public int ErrorCode { get; set; }

    public ResponseStatus ToResponseStatus() => new()
    {
        ErrorCode = GetType().Name,
        Message = Message,
        Errors =
        [
            new()
            {
                ErrorCode = ErrorCode.ToString(),
                Message = Message,
            }
        ]
    };
}