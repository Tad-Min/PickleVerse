using System;
using System.Net;

namespace Entities.Exceptions
{
    public class AppException : Exception
    {
        public HttpStatusCode StatusCode { get; }

        public AppException(string message, HttpStatusCode statusCode = HttpStatusCode.BadRequest)
            : base(message)
        {
            StatusCode = statusCode;
        }

        public AppException(string message, Exception innerException, HttpStatusCode statusCode = HttpStatusCode.InternalServerError)
            : base(message, innerException)
        {
            StatusCode = statusCode;
        }
    }

    public class NotFoundException : AppException
    {
        public NotFoundException(string message)
            : base(message, HttpStatusCode.NotFound)
        {
        }
    }

    public class BadRequestException : AppException
    {
        public BadRequestException(string message)
            : base(message, HttpStatusCode.BadRequest)
        {
        }
    }

    public class UnauthorizedException : AppException
    {
        public UnauthorizedException(string message)
            : base(message, HttpStatusCode.Unauthorized)
        {
        }
    }

    public class ForbiddenException : AppException
    {
        public ForbiddenException(string message)
            : base(message, HttpStatusCode.Forbidden)
        {
        }
    }
}
