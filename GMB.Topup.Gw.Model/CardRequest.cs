using System.Collections.Generic;
using GMB.Topup.Shared;
using GMB.Topup.Shared.Dtos;
using ServiceStack;
using ServiceStack.DataAnnotations;
using System.Runtime.Serialization;
using GMB.Topup.Gw.Model.Dtos;

namespace GMB.Topup.Gw.Model;
// [Route("/api/v1/card/card_push", "POST")]
// public class CardRequestDto : IPost, IReturn<MessageResponseBase>
// {
//     public string Serial { get; set; }
//     public string CardCode { get; set; }
//
//     public string TransCode { get; set; }
//
//     //public byte CardType { get; set; }
//     public int CardValue { get; set; }
//     public string PartnerCode { get; set; }
//     public string Vendor { get; set; }
// }

// [Route("/api/v1/card/cardrequest/status", "PATCH")]
// public class CardRequestUpdateStatus : IPatch, IReturn<MessageResponseBase>
// {
//     public Guid Id { get; set; }
//     public byte CardStatus { get; set; }
// }
[DataContract]
[Route("/api/v1/card/card_sale", "POST")]
public class CardSaleRequest : IPost, IUserInfoRequest, IReturn<NewMessageResponseBase<List<CardRequestResponseDto>>>
{
    [DataMember(Order = 1)] [Required] public string TransCode { get; set; }
    [DataMember(Order = 2)] [Required] public string CategoryCode { get; set; }
    [DataMember(Order = 3)] [Required] public string ServiceCode { get; set; }
    [DataMember(Order = 4)] [Required] public int Quantity { get; set; }
    [DataMember(Order = 5)] [Required] public int CardValue { get; set; }
    [DataMember(Order = 6)] [Required] public string ProductCode { get; set; }
    [DataMember(Order = 7)] public string Email { get; set; }
    [DataMember(Order = 8)] public Channel Channel { get; set; }
    [DataMember(Order = 9)] [Required] public string PartnerCode { get; set; }
    [DataMember(Order = 10)] public string StaffAccount { get; set; }
    [DataMember(Order = 11)] public SystemAccountType AccountType { get; set; }
    [DataMember(Order = 12)] public AgentType AgentType { get; set; }
    [DataMember(Order = 13)] public string ParentCode { get; set; }
}

// [Route("/api/v1/card/card_confirm", "POST")]
// public class CardConfirmRequest : IPost, IReturn<MessageResponseBase>
// {
//     [Required] public Guid Id { get; set; }
//     public decimal CardValue { get; set; }
//     public string Serial { get; set; }
//     public string StockCode { get; set; }
//
//     public Guid CardId { get; set; }
//     public CardConfirmType ConfirmType { get; set; }
// }
//
//
// [Route("/card/callback", "POST")]
// public class CardCallBackRequest : IPost
// {
//     public string ResponseCode { get; set; }
//     public string ResponseMessage { get; set; }
//     public string RequestCode { get; set; }
//     public string TransCode { get; set; }
//     public string Serial { get; set; }
//     public string CardCode { get; set; }
//     public int RequestValue { get; set; }
//     public int CardValue { get; set; }
// }