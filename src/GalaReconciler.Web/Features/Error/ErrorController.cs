using Microsoft.AspNetCore.Mvc;

namespace GalaReconciler.Web.Features.Error;

/// <summary>Friendly error page for unhandled exceptions and error status codes (view: Shared/Views/Error.cshtml).</summary>
public sealed class ErrorController : Controller
{
    [Route("Error/{statusCode:int?}")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Index()
    {
        // Read the code from route data instead of binding a parameter: when this re-executes a request whose
        // form couldn't be read (an upload over the size limit), MVC skips parameter binding altogether.
        ViewData["StatusCode"] = RouteData.Values.TryGetValue("statusCode", out var value) && int.TryParse(value?.ToString(), out var statusCode)
            ? statusCode
            : 500;
        return View("Error");
    }
}
