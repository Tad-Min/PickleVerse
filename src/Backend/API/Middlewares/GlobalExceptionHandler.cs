using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Services.Exceptions;

namespace API.Middlewares
{
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;
        private readonly IHostEnvironment _env;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment env)
        {
            _logger = logger;
            _env = env;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            _logger.LogError(exception, "Lỗi xảy ra tại {Path}: {Message}", httpContext.Request.Path, exception.Message);

            var (statusCode, title) = exception switch
            {
                NotFoundException notFoundEx => ((int)HttpStatusCode.NotFound, notFoundEx.Message),
                BadRequestException badReqEx => ((int)HttpStatusCode.BadRequest, badReqEx.Message),
                UnauthorizedException unauthEx => ((int)HttpStatusCode.Unauthorized, unauthEx.Message),
                ForbiddenException forbidEx => ((int)HttpStatusCode.Forbidden, forbidEx.Message),
                AppException appEx => ((int)appEx.StatusCode, appEx.Message),
                KeyNotFoundException => ((int)HttpStatusCode.NotFound, "Tài nguyên không tìm thấy."),
                UnauthorizedAccessException => ((int)HttpStatusCode.Unauthorized, "Không có quyền truy cập."),
                _ => ((int)HttpStatusCode.InternalServerError, "Đã xảy ra lỗi máy chủ nội bộ. Vui lòng liên hệ quản trị viên.")
            };

            var problemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = exception is AppException ? exception.Message : (_env.IsDevelopment() ? exception.ToString() : "Vui lòng kiểm tra lại yêu cầu hoặc liên hệ hỗ trợ."),
                Instance = httpContext.Request.Path
            };

            problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;
            problemDetails.Extensions["timestamp"] = DateTime.UtcNow;

            httpContext.Response.StatusCode = statusCode;
            httpContext.Response.ContentType = "application/problem+json";

            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
            return true;
        }
    }
}
