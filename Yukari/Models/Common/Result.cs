namespace Yukari.Models.Common
{
    public class Result
    {
        public bool IsSuccess { get; }
        public bool IsCancelled { get; }
        public string? Error { get; }

        protected Result(bool isSuccess, bool isCancelled, string? error)
        {
            IsSuccess = isSuccess;
            IsCancelled = isCancelled;
            Error = error;
        }

        public static Result Success() => new(true, false, null);

        public static Result Failure(string error) => new(false, false, error);

        public static Result Cancelled() => new(false, true, null);
    }

    public class Result<T> : Result
    {
        public T? Value { get; }

        private Result(bool isSuccess, bool isCancelled, T? value, string? error)
            : base(isSuccess, isCancelled, error)
        {
            Value = value;
        }

        public static Result<T> Success(T value) => new(true, false, value, null);

        public static new Result<T> Failure(string error) => new(false, false, default, error);

        public static new Result<T> Cancelled() => new(false, true, default, null);
    }
}
