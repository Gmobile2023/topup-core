using ServiceStack;
using Topup.Shared;

namespace Topup.Balance.Components.Services;

public abstract class AppServiceBase : Service
{
    protected CustomUserSession UserSession => base.SessionAs<CustomUserSession>();
}