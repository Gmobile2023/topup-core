using System.Runtime.Serialization;
using Topup.Shared;
using Topup.Contracts.Requests.Commons;
using ServiceStack;

namespace Topup.Discovery.Requests.Commons;
[DataContract]
[Route("/api/v1/common/tele/send", "Post")]
public class CommonSendMessageTeleRequest : IPost, IReturn<NewMessageResponseBase<object>>
{
   [DataMember(Order = 1)] public string Title { get; set; }
   [DataMember(Order = 2)] public string Message { get; set; }
   [DataMember(Order = 3)] public string Module { get; set; }
  [DataMember(Order = 4)]  public string Code { get; set; }
   [DataMember(Order = 5)] public BotMessageType MessageType { get; set; }
  [DataMember(Order = 6)]  public BotType BotType { get; set; }
}
[DataContract]
[Route("/api/v1/common/tele/send-to-group", "Post")]
public class CommonSendMessageTeleToGroupRequest : CommonSendMessageTeleRequest, IPost, IReturn<NewMessageResponseBase<object>>
{
   [DataMember(Order = 1)] public string ChatId { get; set; }
}