using System;

namespace Topup.Shared.Exceptions;

public class PaygateException : Exception
{
    public PaygateException()
    {
    }

    public PaygateException(string code)
    {
        Code = code;
    }

    public PaygateException(string message, params object[] args)
        : this(string.Empty, message, args)
    {
    }

    public PaygateException(string code, string message, params object[] args)
        : this(null, code, message, args)
    {
    }

    public PaygateException(Exception innerException, string message, params object[] args)
        : this(innerException, string.Empty, message, args)
    {
    }

    public PaygateException(Exception innerException, string code, string message, params object[] args)
        : base(string.Format(message, args), innerException)
    {
        Code = code;
    }

    public string Code { get; }
}