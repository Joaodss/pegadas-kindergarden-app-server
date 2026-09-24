namespace Pegadas.Api.Middleware;

/// <summary>
/// Baseline security headers for a JSON API. Responses under <c>/v1</c> default to
/// <c>Cache-Control: no-store</c> because they carry children's data; endpoints that serve
/// public, cacheable data (e.g. <c>/v1/meta</c>) override it.
/// </summary>
internal sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Cross-Origin-Resource-Policy"] = "same-origin";

        var path = context.Request.Path;

        // The API reference UI and the jobs dashboard need scripts and styles; everything else is JSON.
        if (!path.StartsWithSegments("/scalar") && !path.StartsWithSegments("/ops"))
        {
            headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
        }

        if (path.StartsWithSegments("/v1"))
        {
            headers.CacheControl = "no-store";
        }

        return next(context);
    }
}
