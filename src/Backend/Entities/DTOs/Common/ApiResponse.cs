using System.Text.Json.Serialization;

namespace Entities.DTOs.Common;

public interface IApiResponse
{
    bool Success { get; }
    string Message { get; }
}

public sealed class ApiResponse<T> : IApiResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }

    public static ApiResponse<T> SuccessResponse(T data, string message = "Success") =>
        new() { Success = true, Message = message, Data = data };
    public static ApiResponse<T> ErrorResponse(string message) => new() { Success = false, Message = message };
}

public sealed class ApiResponse : IApiResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Code { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IDictionary<string, string[]>? Errors { get; set; }

    public static ApiResponse SuccessResponse(string message = "Success") => new() { Success = true, Message = message };
    public static ApiResponse ErrorResponse(string message, string? code = null) => new() { Success = false, Message = message, Code = code };
}
