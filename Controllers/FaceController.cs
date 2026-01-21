
using Microsoft.AspNetCore. Mvc;
using FaceCheck.Services;

namespace FaceCheck.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FaceController : ControllerBase
    {
        private readonly IFaceRecognitionService _faceRecognitionService;
        private readonly IAttendanceService _attendanceService;
        private readonly ILogger<FaceController> _logger;

        public FaceController(
            IFaceRecognitionService faceRecognitionService,
            IAttendanceService attendanceService,
            ILogger<FaceController> logger)
        {
            _faceRecognitionService = faceRecognitionService;
            _attendanceService = attendanceService;
            _logger = logger;
        }

        public class FaceDetectRequest
        {
            public string ImageBase64 { get; set; }
        }

        [HttpPost("detect")]
        public async Task<IActionResult> Detect([FromBody] FaceDetectRequest request)
        {
            try
            {
                // 1. Reconnaissance faciale
                var faceResult = await _faceRecognitionService.DetectAndRecognizeAsync(request.ImageBase64);
                
                if (! faceResult.Success || ! faceResult.PersonId.HasValue)
                {
                    return Ok(new 
                    { 
                        success = false, 
                        message = faceResult.Message,
                        faceDetected = faceResult.FaceDetected
                    });
                }

                // 2. Traitement de la présence
                var attendanceResult = await _attendanceService.ProcessAttendanceAsync(faceResult.PersonId.Value);

                return Ok(new
                {
                    success = attendanceResult.Success,
                    message = attendanceResult.Message,
                    name = attendanceResult.PersonName,
                    confidence = faceResult.Confidence,
                    alreadyPresent = attendanceResult.AlreadyPresent,
                    checkInTime = attendanceResult.CheckInTime
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la détection faciale");
                return StatusCode(500, new 
                { 
                    success = false, 
                    message = "Erreur interne du serveur" 
                });
            }
        }

        [HttpPost("retrain")]
        public async Task<IActionResult> RetrainModel()
        {
            try
            {
                await _faceRecognitionService. TrainModelAsync();
                return Ok(new { success = true, message = "Modèle réentraîné avec succès" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du réentraînement");
                return StatusCode(500, new { success = false, message = "Erreur lors du réentraînement" });
            }
        }
    }
}