namespace PRN222_CinemaManagement.Services.IService;

/// <summary>
/// Represents a validation or business error bound to a form field or summary.
/// </summary>
public record ValidationFailure(string Field, string Message);

/// <summary>
/// Standard outcome wrapper returned by service operations to controllers.
/// </summary>
public class ServiceResult
{
    public bool Succeeded { get; protected init; }
    public bool IsNotFound { get; protected init; }
    public string? Message { get; protected init; }
    public List<ValidationFailure> Errors { get; } = [];

    public static ServiceResult Success(string? message = null) =>
        new() { Succeeded = true, Message = message };

    public static ServiceResult NotFound() =>
        new() { Succeeded = false, IsNotFound = true };

    public static ServiceResult Fail(string field, string message)
    {
        var result = new ServiceResult { Succeeded = false };
        result.Errors.Add(new ValidationFailure(field, message));
        return result;
    }

    public static ServiceResult Fail(IEnumerable<ValidationFailure> errors)
    {
        var result = new ServiceResult { Succeeded = false };
        result.Errors.AddRange(errors);
        return result;
    }
}

/// <summary>
/// Outcome wrapper carrying a payload on success.
/// </summary>
public class ServiceResult<T> : ServiceResult
{
    public T? Data { get; private init; }

    public static ServiceResult<T> Success(T data, string? message = null) =>
        new() { Succeeded = true, Data = data, Message = message };

    public new static ServiceResult<T> NotFound() =>
        new() { Succeeded = false, IsNotFound = true };

    public new static ServiceResult<T> Fail(string field, string message)
    {
        var result = new ServiceResult<T> { Succeeded = false };
        result.Errors.Add(new ValidationFailure(field, message));
        return result;
    }

    public new static ServiceResult<T> Fail(IEnumerable<ValidationFailure> errors)
    {
        var result = new ServiceResult<T> { Succeeded = false };
        result.Errors.AddRange(errors);
        return result;
    }
}
