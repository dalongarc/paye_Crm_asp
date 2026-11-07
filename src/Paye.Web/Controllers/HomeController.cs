using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Paye.Web.Controllers;

public sealed class HomeController : Controller
{
    [HttpGet("/")]
    public IActionResult Index()
    {
        var isBrowser = Request.Headers.UserAgent.ToString().Contains("Mozilla", StringComparison.OrdinalIgnoreCase)
                     || Request.Headers.Accept.ToString().StartsWith("text/html", StringComparison.OrdinalIgnoreCase);
        var spaPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "index.html");

        if (isBrowser && System.IO.File.Exists(spaPath) && !Request.Query.ContainsKey("razor"))
        {
            return PhysicalFile(spaPath, "text/html");
        }

        return View();
    }

    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [Route("/error")]
    public IActionResult Error(int? statusCode = null)
    {
        if (statusCode.HasValue && statusCode.Value >= 400)
        {
            Response.StatusCode = statusCode.Value;
        }

        ViewData["StatusCode"] = statusCode ?? HttpContext.Response.StatusCode;
        return View();
    }
}
