using System.Linq;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using AgenticInterview.Api.ActionFilters;

namespace AgenticInterview.Api.Swagger;

public class IdempotencyHeaderOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var hasIdempotentAttribute = context.MethodInfo.DeclaringType.GetCustomAttributes(true)
            .Union(context.MethodInfo.GetCustomAttributes(true))
            .OfType<IdempotentAttribute>()
            .Any();

        if (hasIdempotentAttribute)
        {
            operation.Parameters ??= new System.Collections.Generic.List<IOpenApiParameter>();

            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "X-Idempotency-Key",
                In = ParameterLocation.Header,
                Description = "A unique key to guarantee idempotency of the request. E.g., a GUID.",
                Required = true,
                Schema = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    Format = "uuid"
                }
            });
        }
    }
}
