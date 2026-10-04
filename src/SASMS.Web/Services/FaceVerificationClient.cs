using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SASMS.Web.Security;
using SASMS.Web.Services.Dtos;

namespace SASMS.Web.Services;

public class FaceVerificationClient : IFaceVerificationClient
{
    private static readonly JsonSerializerOptions WireOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly FaceServiceSettings _settings;
    private readonly ILogger<FaceVerificationClient> _logger;

    public FaceVerificationClient(HttpClient httpClient, IOptions<FaceServiceSettings> settings, ILogger<FaceVerificationClient> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<FaceRegisterResult> RegisterFaceAsync(int employeeId, IReadOnlyList<string> images, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = BuildRequest(HttpMethod.Post, "/register", new { employeeId, images });
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadFromJsonAsync<RegisterResponseWire>(WireOptions, cancellationToken);

            if (!response.IsSuccessStatusCode || body is null)
            {
                return new FaceRegisterResult(false, body?.Message ?? "Face registration service returned an unexpected response.");
            }

            return new FaceRegisterResult(body.Success, body.Message ?? "Face registration completed.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Face registration service unreachable for employee {EmployeeId}.", employeeId);
            return new FaceRegisterResult(false, "Face registration service is unavailable right now.");
        }
    }

    public async Task<FaceVerifyResult> VerifyAsync(int employeeId, IReadOnlyList<string> frames, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = BuildRequest(HttpMethod.Post, "/verify", new { employeeId, frames });
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadFromJsonAsync<VerifyResponseWire>(WireOptions, cancellationToken);

            if (!response.IsSuccessStatusCode || body is null)
            {
                return new FaceVerifyResult(false, false, false, body?.Error ?? "Face verification service returned an unexpected response.");
            }

            if (body.Error is not null)
            {
                return new FaceVerifyResult(false, false, false, body.Error);
            }

            return new FaceVerifyResult(true, body.FaceMatchSuccess, body.LivenessPassed, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Face verification service unreachable for employee {EmployeeId}.", employeeId);
            return new FaceVerifyResult(false, false, false, "Face verification service is unavailable right now.");
        }
    }

    public async Task<bool> DeleteFaceAsync(int employeeId, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Delete, new Uri(new Uri(_settings.BaseUrl), $"/faces/{employeeId}"));
            request.Headers.Add("X-Face-Service-Secret", _settings.SharedSecret);
            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Face-service returned {StatusCode} deleting face data for employee {EmployeeId}.", response.StatusCode, employeeId);
                return false;
            }

            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Face-service unreachable while deleting face data for employee {EmployeeId}.", employeeId);
            return false;
        }
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string path, object payload)
    {
        var request = new HttpRequestMessage(method, new Uri(new Uri(_settings.BaseUrl), path))
        {
            Content = JsonContent.Create(payload, options: WireOptions)
        };
        request.Headers.Add("X-Face-Service-Secret", _settings.SharedSecret);
        return request;
    }

    private record RegisterResponseWire(bool Success, string? Message, int SavedCount);
    private record VerifyResponseWire(bool FaceMatchSuccess, bool LivenessPassed, int FramesProcessed, string? Error);
}
