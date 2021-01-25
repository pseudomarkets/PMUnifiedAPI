using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PMUnifiedAPI.Swagger
{
    public class RequiredHeaderParameter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            if (operation.Parameters == null)
            {
                operation.Parameters = new List<OpenApiParameter>();
            }
            
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "UnifiedAuth",
                In = ParameterLocation.Header,
                Description = "User authentication token (JWT)",
                Required = false
            });
        }
    }
}
