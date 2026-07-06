using System.Diagnostics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;

namespace SameDaySocialApp.Infrastructure.Observability;

// 自訂追蹤來源：關鍵服務函式可用 AppTelemetry.Source.StartActivity("名稱") 建立精確計時 span。
public static class AppTelemetry
{
    public const string ServiceName = "tongpin-api";
    public const string ServiceVersion = "1.0.0";
    public static readonly ActivitySource Source = new(ServiceName);
}

// 一站式觀測性：Serilog 結構化日誌 + OpenTelemetry 追蹤/指標，全部走 OTLP 匯出。
// 觀測後端（Seq / Grafana / Jaeger / Datadog…）只由 Observability:OtlpEndpoint 決定，換後端免改程式。
public static class ObservabilityExtensions
{
    public static void AddAppObservability(this WebApplicationBuilder builder)
    {
        var cfg = builder.Configuration;
        var serviceName = cfg["Observability:ServiceName"] ?? AppTelemetry.ServiceName;
        var otlpBase = cfg["Observability:OtlpEndpoint"]?.TrimEnd('/');
        var otlpOn = !string.IsNullOrWhiteSpace(otlpBase);

        builder.Host.UseSerilog((context, config) =>
        {
            config
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.Console();

            if (otlpOn)
            {
                config.WriteTo.OpenTelemetry(o =>
                {
                    o.Endpoint = otlpBase + "/v1/logs";
                    o.Protocol = Serilog.Sinks.OpenTelemetry.OtlpProtocol.HttpProtobuf;
                    o.ResourceAttributes = new Dictionary<string, object>
                    {
                        ["service.name"] = serviceName,
                        ["service.version"] = AppTelemetry.ServiceVersion,
                    };
                });
            }
        });

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName, serviceVersion: AppTelemetry.ServiceVersion))
            .WithTracing(t =>
            {
                t.AddAspNetCoreInstrumentation()
                 .AddHttpClientInstrumentation()
                 .AddSource("Npgsql")                  // DB 查詢 span（Npgsql 內建 ActivitySource）
                 .AddSource(AppTelemetry.ServiceName); // 自訂 span
                if (otlpOn)
                {
                    t.AddOtlpExporter(e =>
                    {
                        e.Endpoint = new Uri(otlpBase + "/v1/traces");
                        e.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                    });
                }
            })
            .WithMetrics(m =>
            {
                m.AddAspNetCoreInstrumentation()
                 .AddHttpClientInstrumentation()
                 .AddRuntimeInstrumentation();
                if (otlpOn)
                {
                    m.AddOtlpExporter(e =>
                    {
                        e.Endpoint = new Uri(otlpBase + "/v1/metrics");
                        e.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                    });
                }
            });
    }

    // 每個 HTTP 請求記一筆結構化計時日誌：方法、路徑、狀態碼、耗時 ms、UserId、TraceId。
    public static void UseAppObservability(this WebApplication app)
    {
        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate = "{RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0} ms";
            options.EnrichDiagnosticContext = (diag, http) =>
            {
                var userId = http.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userId)) diag.Set("UserId", userId);
                diag.Set("Host", http.Request.Host.Value ?? "");
            };
        });
    }
}
