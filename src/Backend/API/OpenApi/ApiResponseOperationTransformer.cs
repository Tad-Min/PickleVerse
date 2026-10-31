using Entities.DTOs.Common;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace API.OpenApi;

[AttributeUsage(AttributeTargets.Method)]
public sealed class FileResponseAttribute : Attribute { }

public sealed class ApiResponseOperationTransformer : IOpenApiOperationTransformer
{
    public async Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        if (context.Description.ActionDescriptor is not ControllerActionDescriptor action) return;
        bool file = action.MethodInfo.IsDefined(typeof(FileResponseAttribute), inherit: true);
        foreach (var response in context.Description.SupportedResponseTypes)
        {
            if (response.StatusCode == 204 || file && response.StatusCode < 400) continue;
            var type = response.Type;
            var envelope = response.StatusCode >= 400 ? typeof(ApiResponse)
                : type == null || type == typeof(void) ? typeof(ApiResponse<object>)
                : typeof(IApiResponse).IsAssignableFrom(type) ? type : typeof(ApiResponse<>).MakeGenericType(type);
            var schema = await context.GetOrCreateSchemaAsync(envelope, cancellationToken: cancellationToken);
            operation.Responses ??= new OpenApiResponses();
            operation.Responses[response.StatusCode.ToString()] = new OpenApiResponse
            {
                Description = response.StatusCode < 400 ? "Success" : "Error",
                Content = new Dictionary<string, OpenApiMediaType> { ["application/json"] = new() { Schema = schema } }
            };
        }
    }
}
