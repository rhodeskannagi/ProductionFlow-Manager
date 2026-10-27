using ProductionFlow.Api.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(_ => new ProductionDb(builder.Configuration.GetConnectionString("ProductionFlow")
    ?? throw new InvalidOperationException("ProductionFlow bağlantı bilgisi eksik.")));
builder.Services.AddSingleton<ProductionService>();
builder.Services.AddSingleton<IntegrityService>();
builder.Services.AddExceptionHandler<ProductionExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();
app.UseExceptionHandler();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/health", async (ProductionDb db, CancellationToken ct) =>
{
    try
    {
        await using var connection = await db.OpenAsync(ct);
        return Results.Ok(new { status = "online", database = "ProductionFlowDemo" });
    }
    catch
    {
        return Results.Problem("SQL Server erişilebilir değil; çevrimdışı demo modunu kullanın.", statusCode: 503);
    }
});

app.MapGet("/api/integrity", (IntegrityService svc, CancellationToken ct) => svc.GetReportAsync(ct));
app.MapGet("/api/overview", (ProductionService svc, CancellationToken ct) => svc.GetOverviewAsync(ct));
app.MapPost("/api/orders/{orderId:int}/preview", (int orderId, QuantityInput body, ProductionService svc, CancellationToken ct) => svc.PreviewProductionAsync(orderId, body.Quantity, ct));
app.MapPost("/api/orders/{orderId:int}/produce", (int orderId, ProduceInput body, ProductionService svc, CancellationToken ct) => svc.ProduceAsync(orderId, body, ct));
app.MapGet("/api/runs/{runId:guid}/undo-preview", (Guid runId, ProductionService svc, CancellationToken ct) => svc.PreviewUndoAsync(runId, ct));
app.MapPost("/api/runs/{runId:guid}/undo", (Guid runId, UndoInput body, ProductionService svc, CancellationToken ct) => svc.UndoAsync(runId, body, ct));

app.Run();
