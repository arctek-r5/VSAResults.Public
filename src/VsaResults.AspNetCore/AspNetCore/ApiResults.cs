using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VsaResults.Errors;
using VsaResults.Features.Features;
using VsaResults.Results;
using VsaResults.VsaResult;

using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace VsaResults.AspNetCore.AspNetCore;

/// <summary>
/// Static helper for converting VsaResult results to ASP.NET Core IResult.
/// Provides consistent HTTP response generation for Minimal APIs.
/// </summary>
public static class ApiResults
{
    /// <summary>
    /// Returns an OK (200) result with the value, or a problem details result on error.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="result">The VsaResult.</param>
    /// <returns>An IResult representing the response.</returns>
    public static IResult Ok<T>(VsaResult<T> result) =>
        result.Match(
            value => Microsoft.AspNetCore.Http.Results.Ok(value),
            errors => ToProblem(errors));

    /// <summary>
    /// Returns a Created (201) result with the value and location, or a problem details result on error.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="result">The VsaResult.</param>
    /// <param name="location">The location of the created resource.</param>
    /// <returns>An IResult representing the response.</returns>
    public static IResult Created<T>(VsaResult<T> result, string location) =>
        result.Match(
            value => Microsoft.AspNetCore.Http.Results.Created(location, value),
            errors => ToProblem(errors));

    /// <summary>
    /// Returns a Created (201) result with the value and a location generated from the value.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="result">The VsaResult.</param>
    /// <param name="locationSelector">Function to generate the location from the value.</param>
    /// <returns>An IResult representing the response.</returns>
    public static IResult Created<T>(VsaResult<T> result, Func<T, string> locationSelector) =>
        result.Match(
            value => Microsoft.AspNetCore.Http.Results.Created(locationSelector(value), value),
            errors => ToProblem(errors));

    /// <summary>
    /// Returns a NoContent (204) result on success, or a problem details result on error.
    /// </summary>
    /// <param name="result">The VsaResult.</param>
    /// <returns>An IResult representing the response.</returns>
    public static IResult NoContent(VsaResult<Success> result) =>
        result.Match(
            _ => Microsoft.AspNetCore.Http.Results.NoContent(),
            errors => ToProblem(errors));

    /// <summary>
    /// Returns a NoContent (204) result on success, or a problem details result on error.
    /// </summary>
    /// <param name="result">The <c>VsaResult&lt;Unit&gt;</c> from side effects.</param>
    /// <returns>An IResult representing the response.</returns>
    public static IResult NoContent(VsaResult<Unit> result) =>
        result.Match(
            _ => Microsoft.AspNetCore.Http.Results.NoContent(),
            errors => ToProblem(errors));

    /// <summary>
    /// Returns an Accepted (202) result with the value, or a problem details result on error.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="result">The VsaResult.</param>
    /// <param name="location">Optional location for the status endpoint.</param>
    /// <returns>An IResult representing the response.</returns>
    public static IResult Accepted<T>(VsaResult<T> result, string? location = null) =>
        result.Match(
            value => Microsoft.AspNetCore.Http.Results.Accepted(location, value),
            errors => ToProblem(errors));

    // ==========================================================================
    // Mutation envelope methods — wrap results in MutationPayload<T>
    // ==========================================================================

    /// <summary>
    /// Returns an OK (200) result wrapped in a <see cref="MutationPayload{T}"/> envelope,
    /// or an enriched problem details result on error.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="result">The VsaResult from the mutation pipeline.</param>
    /// <param name="action">The kebab-case action name for the response envelope.</param>
    /// <returns>An IResult with the mutation payload envelope.</returns>
    public static IResult MutationOk<T>(VsaResult<T> result, string action) =>
        result.Match(
            value => ToMutationPayload(value, action, result.Context),
            errors => ToProblem(errors, action));

    /// <summary>
    /// Returns a Created (200) result wrapped in a <see cref="MutationPayload{T}"/> envelope,
    /// or an enriched problem details result on error.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="result">The VsaResult from the mutation pipeline.</param>
    /// <param name="action">The kebab-case action name for the response envelope.</param>
    /// <returns>An IResult with the mutation payload envelope.</returns>
    public static IResult MutationCreated<T>(VsaResult<T> result, string action) =>
        result.Match(
            value =>
            {
                var payload = BuildMutationPayload(value, action, result.Context);
                return HttpResults.Json(payload, statusCode: StatusCodes.Status201Created);
            },
            errors => ToProblem(errors, action));

    /// <summary>
    /// Returns an OK (200) result with a <see cref="MutationPayload{T}"/> envelope containing
    /// no data, or an enriched problem details result on error.
    /// Replaces NoContent (204) for mutations — the operation receipt IS the content.
    /// </summary>
    /// <param name="result">The VsaResult from the mutation pipeline.</param>
    /// <param name="action">The kebab-case action name for the response envelope.</param>
    /// <returns>An IResult with the mutation payload envelope (no data field).</returns>
    public static IResult MutationNoContent(VsaResult<Unit> result, string action) =>
        result.Match(
            _ => ToMutationPayload<object>(default, action, result.Context),
            errors => ToProblem(errors, action));

    /// <summary>
    /// Returns an OK (200) result with a <see cref="MutationPayload{T}"/> envelope containing
    /// no data, or an enriched problem details result on error.
    /// Replaces NoContent (204) for mutations — the operation receipt IS the content.
    /// </summary>
    /// <param name="result">The VsaResult from the mutation pipeline.</param>
    /// <param name="action">The kebab-case action name for the response envelope.</param>
    /// <returns>An IResult with the mutation payload envelope (no data field).</returns>
    public static IResult MutationNoContent(VsaResult<Success> result, string action) =>
        result.Match(
            _ => ToMutationPayload<object>(default, action, result.Context),
            errors => ToProblem(errors, action));

    /// <summary>
    /// Builds a <see cref="MutationPayload{T}"/> from a successful result, extracting the
    /// <see cref="MutationReceipt"/> from the VsaResult context if present.
    /// </summary>
    private static IResult ToMutationPayload<T>(T? value, string action, IReadOnlyDictionary<string, object> resultContext)
    {
        var payload = BuildMutationPayload(value, action, resultContext);
        return HttpResults.Ok(payload);
    }

    private static MutationPayload<T> BuildMutationPayload<T>(T? value, string action, IReadOnlyDictionary<string, object> resultContext)
    {
        var receipt = MutationPayload.ExtractReceipt(resultContext);
        var clientContext = receipt?.ClientContext is { Count: > 0 } ctx ? ctx : null;
        var message = receipt?.Message ?? MutationPayload.DefaultSuccessMessage(action);

        return new MutationPayload<T>
        {
            Action = action,
            Status = "success",
            Message = message,
            Timestamp = DateTimeOffset.UtcNow,
            Context = clientContext,
            Data = value,
        };
    }

    /// <summary>
    /// Converts an IActionResult to an IResult for Minimal API compatibility.
    /// Useful when feature handlers return IActionResult values that need to be
    /// converted to Minimal API IResult types.
    /// </summary>
    /// <param name="action">The IActionResult to convert.</param>
    /// <returns>An equivalent IResult for Minimal APIs.</returns>
    public static IResult FromActionResult(IActionResult action) => action switch
    {
        OkObjectResult ok => Microsoft.AspNetCore.Http.Results.Ok(ok.Value),
        CreatedResult created => Microsoft.AspNetCore.Http.Results.Created(created.Location ?? string.Empty, created.Value),
        NoContentResult => Microsoft.AspNetCore.Http.Results.NoContent(),
        NotFoundResult => Microsoft.AspNetCore.Http.Results.NotFound(),
        NotFoundObjectResult notFound => Microsoft.AspNetCore.Http.Results.NotFound(notFound.Value),
        BadRequestResult => Microsoft.AspNetCore.Http.Results.BadRequest(),
        BadRequestObjectResult bad => Microsoft.AspNetCore.Http.Results.BadRequest(bad.Value),
        UnauthorizedResult => Microsoft.AspNetCore.Http.Results.Unauthorized(),
        UnauthorizedObjectResult unauth => ToJsonResult(unauth.Value, StatusCodes.Status401Unauthorized),
        ForbidResult => Microsoft.AspNetCore.Http.Results.Forbid(),
        StatusCodeResult status => Microsoft.AspNetCore.Http.Results.StatusCode(status.StatusCode),
        ObjectResult obj => ToJsonResult(obj.Value, obj.StatusCode ?? StatusCodes.Status200OK, obj.ContentTypes.FirstOrDefault()),
        JsonResult json => ToJsonResult(
            json.Value,
            json.StatusCode ?? StatusCodes.Status200OK,
            json.ContentType,
            json.SerializerSettings as JsonSerializerOptions),
        _ => Microsoft.AspNetCore.Http.Results.Ok(action),
    };

    /// <summary>
    /// Converts a list of errors to a Problem Details result enriched with the action name.
    /// </summary>
    /// <param name="errors">The errors to convert.</param>
    /// <param name="action">The kebab-case action name to include in the problem details extensions.</param>
    /// <returns>An IResult representing the problem details with action context.</returns>
    public static IResult ToProblem(IReadOnlyList<Error> errors, string action)
    {
        if (errors.Count == 0)
        {
            return Microsoft.AspNetCore.Http.Results.Problem(statusCode: StatusCodes.Status500InternalServerError);
        }

        var firstError = errors[0];

        if (errors.All(e => e.Type == ErrorType.Validation))
        {
            var errorDictionary = errors
                .GroupBy(e => e.Code)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.Description).ToArray());

            return Microsoft.AspNetCore.Http.Results.ValidationProblem(
                errorDictionary,
                title: "Validation Failed",
                statusCode: StatusCodes.Status400BadRequest,
                extensions: new Dictionary<string, object?> { ["action"] = action });
        }

        var extensions = GetExtensions(errors) ?? [];
        extensions["action"] = action;

        return Microsoft.AspNetCore.Http.Results.Problem(
            statusCode: GetStatusCode(firstError.Type),
            title: GetTitle(firstError.Type),
            detail: firstError.Description,
            extensions: extensions);
    }

    /// <summary>
    /// Converts a list of errors to a Problem Details result.
    /// </summary>
    /// <param name="errors">The errors to convert.</param>
    /// <returns>An IResult representing the problem details.</returns>
    public static IResult ToProblem(IReadOnlyList<Error> errors)
    {
        if (errors.Count == 0)
        {
            return Microsoft.AspNetCore.Http.Results.Problem(statusCode: StatusCodes.Status500InternalServerError);
        }

        var firstError = errors[0];

        // If all errors are validation errors, return a validation problem
        if (errors.All(e => e.Type == ErrorType.Validation))
        {
            var errorDictionary = errors
                .GroupBy(e => e.Code)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.Description).ToArray());

            return Microsoft.AspNetCore.Http.Results.ValidationProblem(
                errorDictionary,
                title: "Validation Failed",
                statusCode: StatusCodes.Status400BadRequest);
        }

        return Microsoft.AspNetCore.Http.Results.Problem(
            statusCode: GetStatusCode(firstError.Type),
            title: GetTitle(firstError.Type),
            detail: firstError.Description,
            extensions: GetExtensions(errors));
    }

    private static int GetStatusCode(ErrorType errorType) => errorType switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.BadRequest => StatusCodes.Status400BadRequest,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Timeout => StatusCodes.Status408RequestTimeout,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Gone => StatusCodes.Status410Gone,
        ErrorType.Locked => StatusCodes.Status423Locked,
        ErrorType.TooManyRequests => StatusCodes.Status429TooManyRequests,
        ErrorType.Failure => StatusCodes.Status500InternalServerError,
        ErrorType.Unexpected => StatusCodes.Status500InternalServerError,
        ErrorType.Unavailable => StatusCodes.Status503ServiceUnavailable,
        _ => StatusCodes.Status500InternalServerError,
    };

    private static string GetTitle(ErrorType errorType) => errorType switch
    {
        ErrorType.Validation => "Bad Request",
        ErrorType.BadRequest => "Bad Request",
        ErrorType.Unauthorized => "Unauthorized",
        ErrorType.Forbidden => "Forbidden",
        ErrorType.NotFound => "Not Found",
        ErrorType.Timeout => "Request Timeout",
        ErrorType.Conflict => "Conflict",
        ErrorType.Gone => "Gone",
        ErrorType.Locked => "Locked",
        ErrorType.TooManyRequests => "Too Many Requests",
        ErrorType.Failure => "Internal Server Error",
        ErrorType.Unexpected => "Internal Server Error",
        ErrorType.Unavailable => "Service Unavailable",
        _ => "An error occurred",
    };

    private static Dictionary<string, object?>? GetExtensions(IReadOnlyList<Error> errors)
    {
        if (errors.Count <= 1)
        {
            return errors.Count == 1
                ? new Dictionary<string, object?> { ["errorCode"] = errors[0].Code }
                : null;
        }

        return new Dictionary<string, object?>
        {
            ["errorCode"] = errors[0].Code,
            ["errors"] = errors.Select(e => new { e.Code, e.Description }).ToArray(),
        };
    }

    private static IResult ToJsonResult(
        object? value,
        int statusCode,
        string? contentType = null,
        JsonSerializerOptions? serializerOptions = null) =>
        Microsoft.AspNetCore.Http.Results.Json(value, serializerOptions, contentType, statusCode);
}
