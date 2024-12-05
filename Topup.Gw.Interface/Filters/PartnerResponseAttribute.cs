using System.Linq;
using System.Threading.Tasks;
using ServiceStack;
using ServiceStack.Web;
using Topup.Shared.Dtos;
using Topup.Shared.Utils;

namespace Topup.Gw.Interface.Filters;

public class PartnerResponseAttribute : ResponseFilterAsyncAttribute
{
    public override Task ExecuteAsync(IRequest req, IResponse res, object responseDto)
    {
        if (responseDto == null) return Task.CompletedTask;
        var properties = responseDto.GetType().GetProperties();
        var signature = properties.FirstOrDefault(p => p.Name.Contains("Sig"));
        var status = properties.FirstOrDefault(p => p.Name.Contains("Status"));
        var data = properties.FirstOrDefault(p => p.Name.Contains("Data"));
        var sign = string.Empty;

        if (data?.PropertyType == typeof(PartnerResult))
        {
            var dataInfo = (PartnerResult)data.GetValue(responseDto, null);

            if (dataInfo != null && status != null)
            {
                var statusCode = status.GetValue(responseDto, null);
                sign = Cryptography.Sign(
                    string.Join("|", statusCode, dataInfo.RequestCode),
                    "GMB_PrivateKey.pem"
                );
            }
        }

        if (signature != null)
            signature.SetValue(responseDto, sign, null);

        return Task.CompletedTask;
    }
}