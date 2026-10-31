using Entities.DTOs.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace API.Filters;

// Apply the same contract to every JSON action, including MVC validation failures.
// FileResult downloads retain their media type and binary/file contents.
public sealed class ApiResponseFilter : IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is ObjectResult result && result.Value is not IApiResponse)
        {
            int status = result.StatusCode ?? 200;
            if (status is >= 200 and < 300)
                result.Value = ApiResponse<object?>.SuccessResponse(result.Value, Message(result.Value) ?? "Success");
            else
            {
                var error = ApiResponse.ErrorResponse(Message(result.Value) ?? DefaultMessage(status), Code(result.Value));
                if (result.Value is ValidationProblemDetails validation)
                {
                    error.Message = string.Join(" ", validation.Errors.Values.SelectMany(v => v).Take(4));
                    error.Code = "validation_failed";
                    error.Errors = validation.Errors;
                }
                result.Value = error;
            }
            result.DeclaredType = result.Value.GetType();
            result.ContentTypes.Clear();
            result.ContentTypes.Add("application/json");
        }
        else if (context.Result is StatusCodeResult statusResult)
        {
            int status = statusResult.StatusCode;
            if (status == 204) return;
            context.Result = new ObjectResult(status < 400 ? ApiResponse.SuccessResponse() : ApiResponse.ErrorResponse(DefaultMessage(status))) { StatusCode = status };
        }
    }

    public void OnResultExecuted(ResultExecutedContext context) { }
    static string? Message(object? value) => value is ProblemDetails problem ? problem.Title : Text(value, "Message", "message");
    static string? Code(object? value) => Text(value, "Code", "code");
    static string? Text(object? value, string first, string second) => value == null ? null :
        (value.GetType().GetProperty(first) ?? value.GetType().GetProperty(second))?.GetValue(value) as string;
    public static string DefaultMessage(int status) => status switch
    {
        400 => "Dữ liệu yêu cầu không hợp lệ.",
        401 => "Vui lòng đăng nhập để tiếp tục.",
        403 => "Bạn không có quyền thực hiện thao tác này.",
        404 => "Không tìm thấy tài nguyên.",
        405 => "Phương thức HTTP không được hỗ trợ.",
        409 => "Dữ liệu đã thay đổi hoặc yêu cầu bị trùng.",
        429 => "Quá nhiều yêu cầu. Vui lòng thử lại sau.",
        _ => "Không thể xử lý yêu cầu."
    };
}
