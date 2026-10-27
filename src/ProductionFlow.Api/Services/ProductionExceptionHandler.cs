using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.SqlClient;

namespace ProductionFlow.Api.Services;


public sealed class ProductionExceptionHandler(ILogger<ProductionExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context,Exception exception,CancellationToken cancellationToken)
    {
        logger.LogError(exception,"ProductionFlow request failed");
        context.Response.StatusCode = exception switch
        {
            DomainException => StatusCodes.Status409Conflict,
            SqlException s when s.Number is 2601 or 2627 => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };
        var message = exception switch
        {
            DomainException => exception.Message,
            SqlException s when s.Number is 2601 or 2627 => "Bu işlem daha önce işlendi veya aynı kayıt zaten var.",
            _ => "Sunucu hatası. İşlem atomik olarak iptal edildi; sistem günlüklerini inceleyin."
        };
        await context.Response.WriteAsJsonAsync(new { error = message },cancellationToken);
        return true;
    }
}
