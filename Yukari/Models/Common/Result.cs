using Yukari.Enums;

namespace Yukari.Models.Common;

public class Result
{
    public ResultKind Kind { get; }

    public bool IsSuccess => Kind == ResultKind.Success;
    public bool IsCancelled => Kind == ResultKind.Cancelled;
    public string? ErrorTitle { get; }
    public string? Error { get; }

    protected Result(ResultKind kind, string? errorTitle, string? error)
    {
        Kind = kind;
        ErrorTitle = errorTitle;
        Error = error;
    }

    public static Result Success() => new(ResultKind.Success, null, null);

    public static Result PendingRestart() => new(ResultKind.PendingRestart, null, null);

    public static Result Cancelled() => new(ResultKind.Cancelled, null, null);

    public static Result Failure(string error, string? errorTitle = null) =>
        new(ResultKind.Failure, errorTitle, error);

    public static Result ComicSourceDisabled(string error, string? errorTitle = null) =>
        new(ResultKind.ComicSourceDisabled, errorTitle, error);
}

public class Result<T> : Result
{
    public T? Value { get; }

    private Result(ResultKind kind, string? errorTitle, string? error, T? value)
        : base(kind, errorTitle, error) => Value = value;

    public static Result<T> Success(T value) => new(ResultKind.Success, null, null, value);

    public static new Result<T> Cancelled() => new(ResultKind.Cancelled, null, null, default);

    public static new Result<T> Failure(string error, string? errorTitle = null) =>
        new(ResultKind.Failure, errorTitle, error, default);

    public static new Result<T> ComicSourceDisabled(string error, string? errorTitle = null) =>
        new(ResultKind.ComicSourceDisabled, errorTitle, error, default);
}
