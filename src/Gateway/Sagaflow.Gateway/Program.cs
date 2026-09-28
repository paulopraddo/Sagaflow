using System.Diagnostics;
using System.Threading.RateLimiting;
using Sagaflow.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Per-client fixed window; routes opt in through "RateLimiterPolicy" in configuration.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("per-client", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue("RateLimiting:PermitLimit", 100),
            Window = TimeSpan.FromSeconds(10),
            QueueLimit = 0,
        }));
});

var app = builder.Build();

// Echo the trace id so a client (or a bug report) can jump straight to the distributed trace in Grafana.
app.Use((context, next) =>
{
    context.Response.OnStarting(() =>
    {
        if (Activity.Current is { } activity)
        {
            context.Response.Headers["X-Trace-Id"] = activity.TraceId.ToString();
        }

        return Task.CompletedTask;
    });
    return next(context);
});

app.UseRateLimiter();
app.MapDefaultEndpoints();
app.MapReverseProxy();

await app.RunAsync();

public partial class Program;
