using FluentValidation;
using MediatR;
namespace StellarSigner.Application.Common;
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var errors = validators.SelectMany(v => v.Validate(request).Errors).Where(e => e is not null).ToArray();
        if (errors.Length != 0) throw new SignerException("INVALID_REQUEST", 400);
        return await next();
    }
}
