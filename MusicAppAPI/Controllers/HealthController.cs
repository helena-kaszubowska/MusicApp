using Microsoft.AspNetCore.Mvc;

namespace MusicAppAPI.Controllers;

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok("OK");
}
