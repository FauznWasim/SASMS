namespace SASMS.Web.Services.Dtos;

/// <summary>Transport-level outcome of a face-service call, distinct from the CV result itself
/// (e.g. Success=false covers "service unreachable" and "no registered template" alike —
/// both mean "we don't have a real answer," which the caller should treat the same way).</summary>
public record FaceRegisterResult(bool Success, string Message);

public record FaceVerifyResult(bool Success, bool FaceMatchSuccess, bool LivenessPassed, string? ErrorMessage);
