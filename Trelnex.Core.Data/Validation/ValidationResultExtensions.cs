using System.Collections.Immutable;
using FluentValidation.Results;

namespace Trelnex.Core.Data;

public static class ValidationResultExtensions
{
    public static void ValidateOrThrow<T>(
        this ValidationResult validationResult)
    {
        if (validationResult.IsValid) return;

        var message = $"The '{typeof(T)}' is not valid.";

        // convert the errors to an array of key-value pairs
        // where the key is the property name and value is the validation error
        var errors = validationResult
            .Errors
            .GroupBy(e => e.PropertyName)
            .ToImmutableSortedDictionary(
                keySelector: g => g
                    .Key,
                elementSelector: g => g
                    .Select(h => h.ErrorMessage)
                    .OrderBy(v => v)
                    .ToArray());

        throw new ValidationException(
            message: message,
            errors: errors);
    }
}
