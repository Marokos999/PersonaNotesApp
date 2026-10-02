using System.ComponentModel.DataAnnotations;

namespace PersonaNotesApp.API.Application.Validation;

/// <summary>Runs DataAnnotations validation on the request body and returns a 400 problem response on failure.</summary>
public class ValidationFilter<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var body = context.Arguments.OfType<T>().FirstOrDefault();
        if (body is null)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["body"] = ["Request body is required."] });

        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(body, new ValidationContext(body), results, validateAllProperties: true))
        {
            var errors = results
                .SelectMany(r => (r.MemberNames.Any() ? r.MemberNames : [string.Empty])
                    .Select(member => (member, message: r.ErrorMessage ?? "Invalid value.")))
                .GroupBy(x => x.member)
                .ToDictionary(g => g.Key, g => g.Select(x => x.message).ToArray());
            return Results.ValidationProblem(errors);
        }

        return await next(context);
    }
}

public static class ValidationExtensions
{
    public static RouteHandlerBuilder WithValidation<T>(this RouteHandlerBuilder builder) where T : class =>
        builder.AddEndpointFilter<ValidationFilter<T>>();
}
