using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using VsaResults.Errors;
using VsaResults.Features.Features;
using VsaResults.VsaResult;

namespace VsaResults.Features.Validation;

/// <summary>
/// Default <see cref="IFeatureValidator{TRequest}"/> implementation that uses FluentValidation when an
/// <see cref="IValidator{T}"/> for <typeparamref name="TRequest"/> is registered.
/// Falls back to a no-op validation pass when no FluentValidation validator exists.
/// </summary>
public sealed class FluentValidationFeatureValidator<TRequest>(IServiceProvider serviceProvider)
    : IFeatureValidator<TRequest>
{
    public async Task<VsaResult<TRequest>> ValidateAsync(TRequest request, CancellationToken ct = default)
    {
        var validator = serviceProvider.GetService<IValidator<TRequest>>();
        if (validator is null)
        {
            return request.ToResult();
        }

        var result = await validator.ValidateAsync(request, ct);
        if (result.IsValid)
        {
            return request.ToResult();
        }

        var errors = result.Errors
            .Where(e => e is not null)
            .Select(e => Error.Validation(
                code: BuildErrorCode(e, typeof(TRequest)),
                description: e.ErrorMessage))
            .ToList();

        return errors;
    }

    private static string BuildErrorCode(FluentValidation.Results.ValidationFailure failure, Type requestType)
    {
        var property = string.IsNullOrWhiteSpace(failure.PropertyName) ? "Request" : failure.PropertyName;
        var rule = string.IsNullOrWhiteSpace(failure.ErrorCode) ? "Invalid" : failure.ErrorCode;
        return $"{requestType.Name}.{property}.{rule}";
    }
}
