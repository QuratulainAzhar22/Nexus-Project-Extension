using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Nexus_backend.Utils;

namespace Nexus_backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class VideoController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public VideoController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet("token")]
        public IActionResult GetToken([FromQuery] string channelName, [FromQuery] uint uid = 0)
        {
            if (string.IsNullOrEmpty(channelName))
                return BadRequest(new { error = "channelName is required" });

            var appId = _configuration["Agora:AppId"];
            var appCertificate = _configuration["Agora:AppCertificate"];

            if (string.IsNullOrEmpty(appId))
                return StatusCode(500, new { error = "Agora AppId not configured" });

            // If no certificate, return null token (Agora test mode)
            if (string.IsNullOrEmpty(appCertificate) || appCertificate == "YOUR_APP_CERTIFICATE_HERE")
            {
                return Ok(new
                {
                    token = (string?)null,
                    appId,
                    channelName,
                    uid
                });
            }

            var token = AgoraTokenBuilder.BuildToken(
                appId,
                appCertificate,
                channelName,
                uid,
                AgoraTokenBuilder.RolePublisher,
                expireSeconds: 3600
            );

            return Ok(new { token, appId, channelName, uid });
        }
    }
}