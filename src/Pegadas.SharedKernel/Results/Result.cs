using System.Diagnostics.CodeAnalysis;

namespace Pegadas.SharedKernel.Results;

/// <summary>Outcome of a use case that returns no value.</summary>
public class Result
{
    private static readonly Result SuccessInstance = new(null);

    protected Result(Error? error) => Error = error;

    public Error? Error { get; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public static Result Success() => SuccessInstance;

    public static Result Failure(Error error) => new(error ?? throw new ArgumentNullException(nameof(error)));

    public static implicit operator Result(Error error) => Failure(error);
}

/// <summary>Outcome of a use case that returns a value on success.</summary>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value)
        : base(null) => _value = value;

    private Result(Error error)
        : base(error)
    {
    }

    /// <summary>The value; throws when the result is a failure.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Result is a failure ({Error.Code}); there is no value.");

#pragma warning disable CA1000 // Static factory on generic type is the intended API.
    public static Result<T> Success(T value) => new(value);

    public static new Result<T> Failure(Error error) => new(error ?? throw new ArgumentNullException(nameof(error)));
#pragma warning restore CA1000

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(Error error) => Failure(error);
}
