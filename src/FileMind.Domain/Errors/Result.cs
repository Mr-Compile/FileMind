using System.Diagnostics.CodeAnalysis;

namespace FileMind.Domain.Errors;

public readonly record struct Result<T>
{
    public T? Value { get; }
    public FileMindError? Error { get; }

    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    private Result(T? value, FileMindError? error)
    {
        Value = value;
        Error = error;
    }

    public static Result<T> Ok(T value) => new(value, null);
    public static Result<T> Fail(FileMindError error) => new(default, error);

    public static implicit operator Result<T>(T value) => Ok(value);
}
