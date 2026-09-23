using Microsoft.AspNetCore.Mvc;

namespace LibraSys.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "ok", service = "LibraSys.Api" });
}
