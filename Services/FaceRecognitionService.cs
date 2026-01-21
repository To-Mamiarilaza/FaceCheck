
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Emgu.CV;
using Emgu.CV.Face;
using FaceCheck.Data;
using FaceCheck.Models;
using FaceCheck.Pages;

namespace FaceCheck.Services
{
    public class FaceRecognitionService : IFaceRecognitionService, IDisposable
    {
        private readonly ILogger<FaceRecognitionService> _logger;
        private readonly CascadeClassifier _faceCascade;
        private readonly AppDbContext _context;
        private readonly LBPHFaceRecognizer _recognizer;
        private readonly IWebHostEnvironment _env;
        private readonly string _modelPath;
        private readonly string _cascadePath;
        private readonly FaceRecognitionSettings _settings;

        public bool IsModelTrained { get; private set; }

        public FaceRecognitionService(
            AppDbContext context, 
            IWebHostEnvironment env,
            IOptions<FaceRecognitionSettings> settings,
            ILogger<FaceRecognitionService> logger)
        {
            _logger = logger;
            _context = context;
            _env = env;
            _settings = settings. Value;

            _cascadePath = Path.Combine(env.ContentRootPath, "haarcascade_frontalface_default.xml");
            _faceCascade = new CascadeClassifier(_cascadePath);

            if (!File.Exists(_cascadePath))
            {
                throw new FileNotFoundException($"Cascade file not found at: {_cascadePath}");
            }

            _recognizer = new LBPHFaceRecognizer(
                _settings.MinFaceSize, 
                _settings.MinNeighbors, 
                _settings.LBPHGridX, 
                _settings.LBPHGridY, 
                _settings.Threshold);

            _modelPath = Path. Combine(_env.ContentRootPath, "Models", "face_model.yml");

            InitializeModel();
        }

        private void InitializeModel()
        {
            if (File.Exists(_modelPath))
            {
                _recognizer.Read(_modelPath);
                IsModelTrained = true;
                _logger.LogInformation("✔ Modèle LBPH chargé");
            }
            else
            {
                Task.Run(async () => await TrainModelAsync());
            }
        }

        public async Task<FaceDetectionResult> DetectAndRecognizeAsync(string imageBase64)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(imageBase64))
                {
                    return new FaceDetectionResult 
                    { 
                        Success = false, 
                        Message = "Image vide",
                        FaceDetected = false 
                    };
                }

                // Décodage Base64
                byte[] imageBytes = Convert.FromBase64String(imageBase64);
                
                using var mat = new Mat();
                CvInvoke. Imdecode(imageBytes, Emgu.CV.CvEnum.ImreadModes.ColorRgb, mat);

                if (mat.IsEmpty)
                {
                    return new FaceDetectionResult 
                    { 
                        Success = false, 
                        Message = "Impossible de lire l'image",
                        FaceDetected = false 
                    };
                }

                // Conversion en gris
                using var gray = new Mat();
                CvInvoke.CvtColor(mat, gray, Emgu.CV.CvEnum.ColorConversion.Bgr2Gray);

                // Détection des visages
                var faces = _faceCascade. DetectMultiScale(
                    gray, 
                    _settings.ScaleFactor, 
                    _settings.MinNeighbors, 
                    new System.Drawing.Size(50, 50));

                if (faces.Length == 0)
                {
                    return new FaceDetectionResult 
                    { 
                        Success = false, 
                        Message = "Aucun visage détecté",
                        FaceDetected = false 
                    };
                }

                if (! IsModelTrained)
                {
                    return new FaceDetectionResult 
                    { 
                        Success = false, 
                        Message = "Modèle non entraîné",
                        FaceDetected = true 
                    };
                }

                var faceRect = faces[0];
                using var faceMat = new Mat(gray, faceRect);

                // Reconnaissance
                var result = _recognizer.Predict(faceMat);

                if (result.Label == -1 || result.Distance > _settings.ConfidenceThreshold)
                {
                    return new FaceDetectionResult 
                    { 
                        Success = false, 
                        Message = "Visage non reconnu",
                        FaceDetected = true,
                        Confidence = 100 - result.Distance 
                    };
                }

                return new FaceDetectionResult
                {
                    Success = true,
                    PersonId = result.Label,
                    FaceDetected = true,
                    Confidence = 100 - result.Distance
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la reconnaissance faciale");
                return new FaceDetectionResult 
                { 
                    Success = false, 
                    Message = $"Erreur traitement image : {ex.Message}",
                    FaceDetected = false 
                };
            }
        }

        public async Task TrainModelAsync()
        {
            try
            {
                await Task.Run(() =>
                {
                    var pictures = _context.PictureDirectory
                        .Include(p => p.Person)
                        .ToList();

                    if (!pictures.Any())
                    {
                        _logger.LogWarning("Aucune image disponible pour l'entraînement");
                        return;
                    }

                    var images = new List<Mat>();
                    var labels = new List<int>();
                    var failedImages = new List<string>();

                    foreach (var pic in pictures)
                    {
                        try
                        {
                            var fullPath = Path.Combine(_env. WebRootPath, pic.Url.TrimStart('/'));

                            if (!File.Exists(fullPath))
                            {
                                _logger.LogWarning($"Image non trouvée:  {fullPath}");
                                failedImages.Add(pic. Url);
                                continue;
                            }

                            var mat = CvInvoke. Imread(fullPath, Emgu.CV.CvEnum. ImreadModes.Grayscale);

                            if (!mat.IsEmpty)
                            {
                                images.Add(mat);
                                labels.Add(pic.Person.Id);
                            }
                            else
                            {
                                _logger. LogWarning($"Impossible de charger l'image: {fullPath}");
                                failedImages.Add(pic.Url);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Erreur lors du traitement de l'image {pic. Url}");
                            failedImages.Add(pic.Url);
                        }
                    }

                    if (failedImages.Any())
                    {
                        _logger.LogWarning($"Échec du chargement de {failedImages.Count} images lors de l'entraînement");
                    }

                    if (images.Count > 0)
                    {
                        _recognizer. Train(images.ToArray(), labels.ToArray());
                        
                        // Sauvegarde du modèle
                        Directory.CreateDirectory(Path.GetDirectoryName(_modelPath)!);
                        _recognizer.Write(_modelPath);
                        
                        IsModelTrained = true;
                        _logger.LogInformation("✔ Modèle LBPH entraîné et sauvegardé");
                    }

                    // Libération de la mémoire
                    foreach (var img in images)
                    {
                        img.Dispose();
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'entraînement du modèle");
                throw;
            }
        }

        public void Dispose()
        {
            _faceCascade?. Dispose();
            _recognizer?.Dispose();
        }
    }
}