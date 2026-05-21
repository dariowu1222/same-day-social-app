namespace SameDaySocialApp.Application.Dto;

public sealed class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Code { get; set; }
    public string? Message { get; set; }
    public string? Warning { get; set; }
    public T? Data { get; set; }

    public static ApiResponse<T> Ok(T data, string? warning = null)
    {
        return new ApiResponse<T> { Success = true, Data = data, Warning = warning };
    }

    public static ApiResponse<T> Fail(string code, string message)
    {
        return new ApiResponse<T> { Success = false, Code = code, Message = message };
    }
}
