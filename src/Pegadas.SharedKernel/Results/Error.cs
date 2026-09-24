namespace Pegadas.SharedKernel.Results;

public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Forbidden,
    Unauthorized,
    TooManyRequests,
    Unexpected,
}

/// <summary>
/// An expected failure. <see cref="Code"/> is stable and machine-readable (the mobile app
/// switches on it); <see cref="Message"/> is for developers and never contains personal data.
/// </summary>
public sealed record Error(string Code, string Message, ErrorType Type)
{
    public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; init; }

    public static Error Validation(string code, string message, IReadOnlyDictionary<string, string[]>? errors = null) =>
        new(code, message, ErrorType.Validation) { ValidationErrors = errors };

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);

    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);

    public static Error TooManyRequests(string code, string message) => new(code, message, ErrorType.TooManyRequests);

    public static Error Unexpected(string code, string message) => new(code, message, ErrorType.Unexpected);
}
