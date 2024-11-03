using System;
using ServiceStack;

namespace GMB.Topup.Kpp.Interface.Dto;

[Route("/api/v1/kpp/PaymentRequest", "POST")]
public class KppPaymentRequest
{
    public DateTime ExportDate { get; set; }

    public string AccountCode { get; set; }
}

[Route("/api/v1/kpp/TransferRequest", "POST")]
public class KppTransferRequest
{
    public DateTime ExportDate { get; set; }

    public string AccountCode { get; set; }
}

[Route("/api/v1/kpp/AccountRequest", "POST")]
public class KppAccountRequest
{
    public DateTime ExportDate { get; set; }

    public string AccountCode { get; set; }
}

[Route("/api/v1/kpp/ExportFileRequest", "POST")]
public class ExportFileRequest
{
    public DateTime date { get; set; }
}

[Route("/api/v1/kpp/RegisterRequest", "POST")]
public class RegisterRequest
{
    public string Name { get; set; }
    public string Code { get; set; }
    public string Content { get; set; }
    public string EmailSend { get; set; }
    public string EmailCC { get; set; }
    public bool IsAuto { get; set; }
    public string AccountList { get; set; }
    public string Providers { get; set; }
}

[Route("/api/v1/kpp/RegisterRequest", "GET")]
public class GetRegisterRequest
{
    public string Code { get; set; }
}

[Route("/api/v1/kpp/AccountInfoRequest", "GET")]
public class GetAccountInfoRequest
{
    public string AccountCode { get; set; }
}