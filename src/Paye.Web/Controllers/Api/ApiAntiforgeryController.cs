using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Paye.Web.Controllers.Api;

[ApiController]
[Route("api/antiforgery")]
public sealed class ApiAntiforgeryController(IAntiforgery antiforgery) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetToken()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new AntiforgeryTokenResponse(tokens.RequestToken ?? string.Empty));
    }
}

public sealed record AntiforgeryTokenResponse(string Token);
