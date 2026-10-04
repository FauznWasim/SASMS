using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using SASMS.Web.Models.ApiDtos;
using SASMS.Web.Security;
using SASMS.Web.Services;

namespace SASMS.Web.Controllers.Api;

/// <summary>Service-to-service callback for an external/decoupled verification caller — not
/// used by the interactive check-in/out flow. Not part of the cookie-authenticated browser
/// session: the caller authenticates with a shared secret header instead, since it is a
/// backend process, not a logged-in user.</summary>
[ApiController]
[Route("api/verification")]
[AllowAnonymous]
[IgnoreAntiforgeryToken]
[EnableRateLimiting("verification-api")]
public class VerificationController : ControllerBase
{
    private readonly IAttendanceService _attendanceService;
    private readonly VerificationServiceSettings _settings;
    private readonly ILogger<VerificationController> _logger;

    public VerificationController(
        IAttendanceService attendanceService,
        IOptions<VerificationServiceSettings> settings,
        ILogger<VerificationController> logger)
    {
        _attendanceService = attendanceService;
        _settings = settings.Value;
        _logger = logger;
    }

    // Idempotent by design: calling this twice with the same payload just re-applies the
    // same VerificationStatus update, so a caller that retries after a dropped response
    // (rather than an actual duplicate verification attempt) can't corrupt state.
    [HttpPost("callback")]
    public async Task<IActionResult> Callback(
        [FromBody] VerificationCallbackDto dto,
        [FromHeader(Name = "X-Verification-Secret")] string? secret)
    {
        if (string.IsNullOrEmpty(_settings.SharedSecret) || !SecretMatches(secret, _settings.SharedSecret))
        {
            _logger.LogWarning("Rejected verification callback with an invalid shared secret from {RemoteIp}.",
                HttpContext.Connection.RemoteIpAddress);
            return Unauthorized();
        }

        var result = await _attendanceService.ApplyVerificationResultAsync(
            dto.EmployeeId, dto.AttendanceDate, dto.FaceMatchSuccess, dto.LivenessPassed);

        if (!result.Success)
        {
            return NotFound(new { error = result.Error });
        }

        return Ok();
    }

    private static bool SecretMatches(string? provided, string expected)
    {
        if (string.IsNullOrEmpty(provided))
        {
            return false;
        }

        var providedBytes = Encoding.UTF8.GetBytes(provided);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);

        // Constant-time comparison to avoid leaking the secret via response-timing side channels.
        if (providedBytes.Length != expectedBytes.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }
}
