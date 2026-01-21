using Emgu.CV;
using FaceCheck.Models;

namespace FaceCheck.Services
{
    public interface IFaceRecognitionService
    {
        Task<FaceDetectionResult> DetectAndRecognizeAsync(string imageBase64);
        Task TrainModelAsync();
        bool IsModelTrained { get; }
    }

    public class FaceDetectionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int?  PersonId { get; set; }
        public double Confidence { get; set; }
        public bool FaceDetected { get; set; }
    }
}