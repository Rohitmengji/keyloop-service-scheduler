using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.EntityFrameworkCore;
using ServiceScheduler.Api.Hosting;
using ServiceScheduler.Api.Http;
using ServiceScheduler.Application;
using ServiceScheduler.Infrastructure;
using ServiceScheduler.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.AddServerHeader = false;
    // No endpoint takes more than a small JSON body (booking is capped lower still at 16 KB).
    kestrel.Limits.MaxRequestBodySize = 32 * 1024;
});
builder.Logging.Configure(logging =>
    logging.ActivityTrackingOptions = ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId);

builder.Services
    .AddApplication()
    .AddInfrastructure();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddOptions<SchedulingOptions>()
    .BindConfiguration(SchedulingOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddControllers(mvc =>
    {
        // A malformed body already yields a precise "$.field" error; skip the generic "request field is required" one.
        mvc.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
        // Report validation errors under the camelCase JSON names clients actually send.
        mvc.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider());
    })
    .AddJsonOptions(json =>
    {
        json.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        json.JsonSerializerOptions.Converters.Add(new ExplicitOffsetDateTimeOffsetConverter());
    });
builder.Services.AddProblemDetails(problems => problems.CustomizeProblemDetails = ProblemDetailsConventions.Apply);
builder.Services.AddExceptionHandler<UnhandledExceptionHandler>();
builder.Services.AddOpenApi(openApi => openApi.AddDocumentTransformer((document, _, _) =>
{
    document.Info.Title = "Keyloop Unified Service Scheduler API";
    document.Info.Description =
        "Books dealership service appointments, guaranteeing a free service bay and a free qualified technician "
        + "for the whole service duration. Errors use RFC 9457 problem details with a stable `code` member.";
    return Task.CompletedTask;
}));
builder.Services.AddHealthChecks()
    .AddDbContextCheck<SchedulerDbContext>(
        "database",
        tags: ["ready"],
        customTestQuery: async (db, ct) =>
            await db.Database.CanConnectAsync(ct) && !(await db.Database.GetPendingMigrationsAsync(ct)).Any());
builder.Services.AddSchedulerOpenTelemetry(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(ui =>
    {
        ui.SwaggerEndpoint("/openapi/v1.json", "Service Scheduler API v1");
        ui.DocumentTitle = "Service Scheduler API";
    });
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

await app.InitializeDatabaseAsync();
await app.RunAsync();
