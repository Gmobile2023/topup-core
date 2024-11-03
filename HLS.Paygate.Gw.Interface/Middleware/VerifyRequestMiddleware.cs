using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace HLS.Paygate.Gw.Interface.Middleware
{
    public class VerifyRequestMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<VerifyRequestMiddleware> _logger;

        public VerifyRequestMiddleware(RequestDelegate next, ILogger<VerifyRequestMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
        }
    }
}
