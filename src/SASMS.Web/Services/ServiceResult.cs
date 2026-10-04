namespace SASMS.Web.Services;

public class ServiceResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }

    public static ServiceResult Ok() => new() { Success = true };
    public static ServiceResult Fail(string error) => new() { Success = false, Error = error };
}

public class ServiceResult<T> : ServiceResult
{
    public T? Data { get; init; }

    public static ServiceResult<T> Ok(T data) => new() { Success = true, Data = data };
    public static new ServiceResult<T> Fail(string error) => new() { Success = false, Error = error };
}
