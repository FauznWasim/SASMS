using SASMS.Web.Services.Dtos;

namespace SASMS.Web.Services;

/// <summary>Typed client for the Python/OpenCV face-service (face-service/), called server-to-server only.</summary>
public interface IFaceVerificationClient
{
    Task<FaceRegisterResult> RegisterFaceAsync(int employeeId, IReadOnlyList<string> images, CancellationToken cancellationToken = default);
    Task<FaceVerifyResult> VerifyAsync(int employeeId, IReadOnlyList<string> frames, CancellationToken cancellationToken = default);

    /// <summary>Deletes an employee's stored face data (reference photos) and has the face-
    /// service rebuild its LBPH model from everyone else's remaining data. Returns true only on
    /// a confirmed success response — callers that need to know whether the biometric data was
    /// actually removed (e.g. before clearing Employee.FaceTemplateRef) must check the result
    /// rather than assume success; this is no longer purely best-effort now that
    /// EmployeeService.ResetFaceRegistrationAsync depends on the outcome.</summary>
    Task<bool> DeleteFaceAsync(int employeeId, CancellationToken cancellationToken = default);
}
