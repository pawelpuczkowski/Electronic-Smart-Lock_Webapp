using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace ZamekDoDrzwi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            return new JsonResult(new
            {
                message = "API działa!",
                time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            });
        }

        [HttpPost]
        public IActionResult Post([FromBody] JsonElement body)
        {
            string value = body.TryGetProperty("test", out var val) ? val.GetString() ?? "brak" : "brak";

            return new JsonResult(new
            {
                received = value,
                message = "POST odebrany poprawnie ✅",
                time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            });
        }
    }
}
