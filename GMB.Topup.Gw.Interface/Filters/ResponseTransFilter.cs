using System.Linq;
using System.Threading.Tasks;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.Utils;
using ServiceStack;
using ServiceStack.Web;

namespace HLS.Paygate.Gw.Interface.Filters
{
    public class ResponseTransFilterAttribute : ResponseFilterAsyncAttribute
    {
        public override async Task ExecuteAsync(IRequest req, IResponse res, object responseDto)
        {
            if (responseDto != null)
            {
                var properties = responseDto.GetType().GetProperties();
                var signature = properties.FirstOrDefault(p => p.Name.Contains("Signature"));
                var responseStatus = properties.FirstOrDefault(p => p.Name.Contains("ResponseStatus"));
                var sign = string.Empty;
                if (responseStatus?.PropertyType == typeof(ResponseStatusApi))
                {
                    var resStatus = (ResponseStatusApi) responseStatus.GetValue(responseDto, null);

                    if (resStatus != null)
                        sign = Cryptography.Sign(string.Join("|", resStatus.ErrorCode, resStatus.TransCode),"NT_PrivateKey.pem");
                }
                if (signature != null)
                {
                    signature.SetValue(responseDto, sign, null);
                }
            }
        }
    }
}
