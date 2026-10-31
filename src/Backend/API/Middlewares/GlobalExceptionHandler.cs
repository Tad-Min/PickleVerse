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
using Entities.Exceptions;
using Entities.DTOs.Common;

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
                Exception error when Services.ServiceErrors.IsUnavailable(error) => (503, "Dịch vụ tạm thời chưa sẵn sàng. Vui lòng thử lại."),
                AppException appEx => ((int)appEx.StatusCode, appEx.Message),
                KeyNotFoundException missing => ((int)HttpStatusCode.NotFound, missing.Message),
                UnauthorizedAccessException access => ((int)HttpStatusCode.Forbidden, access.Message),
                ArgumentException argument => ((int)HttpStatusCode.BadRequest, argument.Message),
                _ => ((int)HttpStatusCode.InternalServerError, "Đã xảy ra lỗi máy chủ nội bộ. Vui lòng liên hệ quản trị viên.")
            };

            httpContext.Response.StatusCode = statusCode;
            await httpContext.Response.WriteAsJsonAsync(ApiResponse.ErrorResponse(title), cancellationToken);
            return true;
        }
    }
}
