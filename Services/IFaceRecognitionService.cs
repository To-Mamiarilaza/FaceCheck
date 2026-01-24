using Emgu.CV;

namespace FaceCheck.Services
{
    public interface IFaceRecognitionService
    {
        Task<FaceDetectionResult> DetectAndRecognizeAsync(string imageBase64);
        Task TrainModelAsync();
        Task<DiagnosticResult> RunDiagnosticsAsync();
        bool IsModelTrained { get; }
    }

    public class FaceDetectionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? PersonId { get; set; }
        public double Confidence { get; set; }
        public bool FaceDetected { get; set; }
    }

    public class DiagnosticResult
    {
        public bool ModelFileExists { get; set; }
        public long ModelFileSize { get; set; }
        public int TotalImages { get; set; }
        public int TotalPersons { get; set; }
        public Dictionary<int, int> ImagesPerPerson { get; set; } = new();
        public int ExistingImageFiles { get; set; }
        public List<string> MissingImageFiles { get; set; } = new();
        public bool IsModelLoaded { get; set; }
        public string? Error { get; set; }
    }
}