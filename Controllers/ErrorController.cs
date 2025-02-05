using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;

namespace RobloxGameServerAPI.Controllers
{
    [ApiController]
    [Route("api/error")] // Route for production error handling
    [ApiExplorerSettings(IgnoreApi = true)] // Exclude from Swagger documentation
    public class ErrorController : ControllerBase
    {
        private readonly ILogger<ErrorController> _logger;

        public ErrorController(ILogger<ErrorController> logger)
        {
            _logger = logger;
        }

        [Route("")]
        [HttpGet]
        [HttpPost]
        [HttpPut]
        [HttpDelete]
        [HttpPatch]
        [HttpHead]
        [HttpOptions]
        public IActionResult Error()
        {
            var exceptionDetails = HttpContext.Features.Get<IExceptionHandlerFeature>();

            if (exceptionDetails != null)
            {
                var exception = exceptionDetails.Error;
                string errorId = Guid.NewGuid().ToString(); // Generate a unique error ID for tracking

                _logger.LogError(exception, "Production error occurred. ErrorID={ErrorID}, Path={Path}", errorId, HttpContext.Request.Path); // Log full exception with ErrorID

                // Return a user-friendly error response with ProblemDetails and ErrorID
                return Problem(
                    detail: "An unexpected error occurred. Please contact support with the Error ID for assistance.",
                    title: "Internal Server Error",
                    statusCode: 500,
                    extensions: new Dictionary<string, object> { { "errorId", errorId } } // Include ErrorID in response extensions
                );
            }

            // If no exception details (shouldn't happen if ExceptionHandlerMiddleware is configured correctly)
            return Problem(statusCode: 500, title: "Internal Server Error", detail: "An unexpected server error occurred.");
        }
    }
}
