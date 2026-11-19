using IResult = Ardalis.Result.IResult;
using Planning.Server.Errors;

namespace Planning.Server.Processors;

/// <summary>
/// Sends HTTP responses globally after processing by mapping <see cref="IResult"/> 
/// status codes to corresponding HTTP status codes, and writing them to the response.
/// </summary>
sealed class GlobalResponseSender : IGlobalPostProcessor
{
    public async Task PostProcessAsync(IPostProcessorContext ctx, CancellationToken ct)
    {
        if (ctx.HttpContext.ResponseStarted())
            return;

        // Als de respons geen Ardalis.Result is, stuurt de code die respons gewoon door
        if (ctx.Response is not IResult result)
        {
            await ctx.HttpContext.Response.SendAsync(ctx.Response, cancellation: ct);
            return;
        }
        
        // succes en fouten worden verschillend verstuurd
        // verstuurt het bestaande Ardalis.Result door
        switch (result.Status)
        {
            case ResultStatus.Ok:
                await ctx.HttpContext.Response.SendAsync(
                    result, StatusCodes.Status200OK, cancellation: ct);
                return;
            case ResultStatus.Created:
                await ctx.HttpContext.Response.SendAsync(
                    result, StatusCodes.Status201Created, cancellation: ct);
                return;
            case ResultStatus.NoContent:
                await ctx.HttpContext.Response.SendAsync(
                    result, StatusCodes.Status204NoContent, cancellation: ct);
                return;
        }

        var status = result.Status switch
        {
            ResultStatus.Invalid => StatusCodes.Status400BadRequest,
            ResultStatus.Unauthorized => StatusCodes.Status401Unauthorized,
            ResultStatus.Forbidden => StatusCodes.Status403Forbidden,
            ResultStatus.NotFound => StatusCodes.Status404NotFound,
            ResultStatus.Conflict => StatusCodes.Status409Conflict,
            ResultStatus.Error => StatusCodes.Status422UnprocessableEntity,
            ResultStatus.CriticalError => StatusCodes.Status500InternalServerError,
            ResultStatus.Unavailable => StatusCodes.Status503ServiceUnavailable,
            _ => throw new ArgumentOutOfRangeException(
                nameof(result.Status), result.Status, "Unsupported result status.")
        };
        
        // maakt veldfouten voor de Problem Details-body, maar alleen bij ResultStatus.Invalid
        IReadOnlyList<ApiFieldError>? errors = result.Status == ResultStatus.Invalid
            ? result.ValidationErrors
                .Select(error => ApiFieldError.From(
                    error.Identifier, error.ErrorMessage))
                .ToArray()
            : null;
        
        var writer = ctx.HttpContext.RequestServices
            .GetRequiredService<ApiProblemResponse>();
        
        await writer.WriteAsync(ctx.HttpContext, writer.Create(ctx.HttpContext, status, errors), ct);
    }
}