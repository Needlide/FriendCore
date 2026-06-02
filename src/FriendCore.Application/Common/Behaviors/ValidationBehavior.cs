using ErrorOr;
using FluentValidation;
using MediatR;

namespace FriendCore.Application.Common.Behaviors;

/// <summary>
///     Validates MediatR requests that return <see cref="ErrorOr{TValue}" />.
///     TValue is the success payload (e.g. AuthResponse); the pipeline response is always ErrorOr&lt;TValue&gt;.
/// </summary>
public sealed class ValidationBehavior<TRequest, TValue>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, ErrorOr<TValue>>
    where TRequest : IRequest<ErrorOr<TValue>>
    where TValue : notnull
{
    public async Task<ErrorOr<TValue>> Handle(
        TRequest request,
        RequestHandlerDelegate<ErrorOr<TValue>> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
            return await next(cancellationToken);

        var context = new ValidationContext<TRequest>(request);
        var validationResults = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(result => result.Errors)
            .Select(failure => Error.Validation(
                failure.ErrorCode,
                failure.ErrorMessage))
            .Distinct()
            .ToList();

        if (failures.Count > 0)
            return failures;

        return await next(cancellationToken);
    }
}