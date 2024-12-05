using ServiceStack;
using Topup.Shared;

namespace Topup.Gw.Interface.Services;

public abstract class AppServiceBase : Service
{
    protected CustomUserSession UserSession => base.SessionAs<CustomUserSession>();
}