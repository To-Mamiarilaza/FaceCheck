using Microsoft.AspNetCore.Mvc;
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

        // Classe pour la requête frontend
        public class FaceDetectRequest
        {
            public string ImageBase64 { get; set; } = string.Empty;
        }

        /// <summary>
        /// Détecte et reconnaît un visage, puis enregistre la présence
        /// </summary>
        [HttpPost("detect")]
        public async Task<IActionResult> Detect([FromBody] FaceDetectRequest request)
        {
            try
            {
                _logger.LogInformation("Réception d'une demande de détection faciale");

                // Étape 1: Détection et reconnaissance faciale
                var faceResult = await _faceRecognitionService.DetectAndRecognizeAsync(request.ImageBase64);
                
                if (!faceResult.Success || !faceResult.PersonId.HasValue)
                {
                    _logger.LogInformation("Échec de la reconnaissance: {Message}", faceResult.Message);
                    return Ok(new 
                    { 
                        success = false, 
                        message = faceResult.Message,
                        faceDetected = faceResult.FaceDetected
                    });
                }

                // Étape 2: Traitement de la présence
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

        /// <summary>
        /// Réentraîne le modèle de reconnaissance faciale
        /// </summary>
        [HttpPost("retrain")]
        public async Task<IActionResult> RetrainModel()
        {
            try
            {
                _logger.LogInformation("Demande de réentraînement du modèle");
                
                await _faceRecognitionService.TrainModelAsync();
                
                return Ok(new 
                { 
                    success = true, 
                    message = "Modèle réentraîné avec succès" 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du réentraînement du modèle");
                return StatusCode(500, new 
                { 
                    success = false, 
                    message = "Erreur lors du réentraînement" 
                });
            }
        }

        /// <summary>
        /// Vérifie l'état du modèle
        /// </summary>
        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            return Ok(new
            {
                modelTrained = _faceRecognitionService.IsModelTrained,
                message = _faceRecognitionService.IsModelTrained 
                    ? "Modèle prêt" 
                    : "Modèle non entraîné"
            });
        }
    }
}