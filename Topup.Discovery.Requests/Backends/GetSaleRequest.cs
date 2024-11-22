using System.Runtime.Serialization;
using Topup.Gw.Model.Dtos;
using ServiceStack;

namespace Topup.Discovery.Requests.Backends;
[DataContract]
[Route("/api/v1/topup", "GET")]
public class GetSaleRequest : IGet, IReturn<SaleRequestDto>
{
  [DataMember(Order = 1)]  public string Filter { get; set; }
}


[DataContract]
[Route("/api/v1/update_card_code", "POST")]
public class UpdateCardCodeRequest : IPost, IReturn<string>
{
    [DataMember(Order = 1)] public string TransCode { get; set; }
}

[Route("/api/v1/test-backend", "POST")]
public class Backend_test_Request : IPost, IReturn<string>
{
    public string Type { get; set; }
    public string Data { get; set; }
}