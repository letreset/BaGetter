using System;
using System.Threading.Tasks;
using BaGetter.Core.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace BaGetter.Web.Middleware;

/// <summary>
/// Adds baseline security headers to every response.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly bool _enabled;

    public SecurityHeadersMiddleware(RequestDelegate next, IOptions<BaGetterOptions> options)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        ArgumentNullException.ThrowIfNull(options);

        _enabled = options.Value.SecurityHeaders?.Enabled ?? true;
    }

    public Task Invoke(HttpContext context)
    {
        if (_enabled)
        {
            context.Response.OnStarting(static state =>
            {
                var headers = ((HttpResponse)state).Headers;
                headers.XContentTypeOptions = "nosniff";
                headers.XFrameOptions = "SAMEORIGIN";
                headers["Referrer-Policy"] = "no-referrer";
                headers["X-Permitted-Cross-Domain-Policies"] = "none";
                headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
                return Task.CompletedTask;
            }, context.Response);
        }

        return _next(context);
    }
}
