namespace SASMS.Web.Models.ApiDtos;

/// <summary>Posted as JSON (not a form) by wwwroot/js/face-capture.js after a webcam burst capture.</summary>
public class FaceFramesSubmission
{
    public List<string> Frames { get; set; } = new();
}
