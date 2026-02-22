using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Emgu.CV;
using Emgu.CV.Face;
using Emgu.CV.CvEnum;
using FaceCheck.Data;
using FaceCheck.Models;
using FaceCheck.Pages;

namespace FaceCheck.Services
{
    public class FaceRecognitionService : IFaceRecognitionService, IDisposable
    {
        private readonly ILogger<FaceRecognitionService> _logger;
        private readonly CascadeClassifier _faceCascade;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly LBPHFaceRecognizer _recognizer;
        private readonly IWebHostEnvironment _env;
        private readonly string _modelPath;
        private readonly string _cascadePath;
        private readonly FaceRecognitionSettings _settings;

        // SemaphoreSlim(1,1) garantit qu'une seule opération OpenCV s'exécute à la fois (non thread-safe)
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        private const int STANDARD_FACE_WIDTH = 120;
        private const int STANDARD_FACE_HEIGHT = 120;

        public bool IsModelTrained { get; private set; }

        public FaceRecognitionService(
            IServiceScopeFactory scopeFactory,
            IWebHostEnvironment env,
            ILogger<FaceRecognitionService> logger,
            IOptions<FaceRecognitionSettings> settings)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _env = env;
            _settings = settings.Value;

            _cascadePath = Path.Combine(env.ContentRootPath, "haarcascade_frontalface_default.xml");

            if (!File.Exists(_cascadePath))
            {
                _logger.LogError("Cascade file not found at: {Path}", _cascadePath);
                throw new FileNotFoundException($"Cascade file not found at: {_cascadePath}");
            }

            _faceCascade = new CascadeClassifier(_cascadePath);

            _recognizer = new LBPHFaceRecognizer(
                _settings.LBPHRadius,
                _settings.LBPHNeighbors,
                _settings.LBPHGridX,
                _settings.LBPHGridY,
                _settings.Threshold);

            _modelPath = Path.Combine(_env.ContentRootPath, "Models", "face_model.yml");

            InitializeModel();
        }

        private void InitializeModel()
        {
            try
            {
                if (File.Exists(_modelPath))
                {
                    _recognizer.Read(_modelPath);
                    IsModelTrained = true;
                    _logger.LogInformation("Modèle LBPH chargé depuis {Path}", _modelPath);
                    ValidateModel();
                }
                else
                {
                    _logger.LogInformation("Aucun modèle trouvé. Entraînement nécessaire.");
                    IsModelTrained = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'initialisation du modèle");
                IsModelTrained = false;
            }
        }

        private void ValidateModel()
        {
            try
            {
                var testMat = new Mat(STANDARD_FACE_HEIGHT, STANDARD_FACE_WIDTH, DepthType.Cv8U, 1);
                testMat.SetTo(new Emgu.CV.Structure.MCvScalar(128));
                var testResult = _recognizer.Predict(testMat);
                _logger.LogInformation("Validation du modèle: Label={Label}, Distance={Distance}",
                    testResult.Label, testResult.Distance);
                testMat.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Le modèle semble corrompu ou invalide");
                IsModelTrained = false;
            }
        }

        /// <summary>
        /// Normalise un visage : redimensionnement + CLAHE (normalisation lumière locale adaptative).
        /// CLAHE est plus robuste que EqualizeHist pour les visages sous éclairage non uniforme.
        /// Doit recevoir uniquement le crop du visage, pas l'image complète.
        /// </summary>
        private Mat NormalizeFace(Mat faceMat)
        {
            var resized = new Mat();
            CvInvoke.Resize(faceMat, resized,
                new System.Drawing.Size(STANDARD_FACE_WIDTH, STANDARD_FACE_HEIGHT));

            // CLAHE : égalisation d'histogramme adaptative locale (meilleure que EqualizeHist global)
            // clipLimit=2 évite l'amplification du bruit, tileGridSize=8x8 couvre les zones du visage
            var result = new Mat();
            CvInvoke.CLAHE(resized, 2.0, new System.Drawing.Size(8, 8), result);
            resized.Dispose();
            return result;
        }

        /// <summary>
        /// Génère des variantes augmentées d'un visage normalisé pour enrichir l'entraînement.
        /// Retourne : original + flip horizontal + luminosité +30 + luminosité -30.
        /// </summary>
        private List<Mat> AugmentFace(Mat normalizedFace)
        {
            var variants = new List<Mat>();

            // 1. Original
            var original = new Mat();
            normalizedFace.CopyTo(original);
            variants.Add(original);

            // 2. Miroir horizontal (simule variation gauche/droite)
            var flipped = new Mat();
            CvInvoke.Flip(normalizedFace, flipped, Emgu.CV.CvEnum.FlipType.Horizontal);
            variants.Add(flipped);

            // 3. Luminosité +30 (simule environnement plus éclairé)
            var brighter = new Mat();
            CvInvoke.ConvertScaleAbs(normalizedFace, brighter, 1.0, 30);
            variants.Add(brighter);

            // 4. Luminosité -30 (simule environnement moins éclairé)
            var darker = new Mat();
            CvInvoke.ConvertScaleAbs(normalizedFace, darker, 1.0, -30);
            variants.Add(darker);

            return variants;
        }

        public async Task<FaceDetectionResult> DetectAndRecognizeAsync(string imageBase64)
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

            Mat? mat = null;
            Mat? gray = null;
            Mat? faceMat = null;
            Mat? normalizedFace = null;

            try
            {
                byte[] imageBytes = Convert.FromBase64String(imageBase64);

                // Sauvegarde de la capture (utilise WebRootPath, cohérent avec le reste du service)
                var uploadsPath = Path.Combine(_env.WebRootPath, "captures");
                Directory.CreateDirectory(uploadsPath);
                var fileName = $"{DateTime.Now:yyyy-MM-dd-HH-mm-ss}-{Guid.NewGuid()}.jpg";
                await System.IO.File.WriteAllBytesAsync(Path.Combine(uploadsPath, fileName), imageBytes);

                // Sérialisation des opérations OpenCV (non thread-safe)
                await _semaphore.WaitAsync();
                try
                {
                    mat = new Mat();
                    CvInvoke.Imdecode(imageBytes, ImreadModes.ColorRgb, mat);

                    if (mat.IsEmpty)
                    {
                        return new FaceDetectionResult
                        {
                            Success = false,
                            Message = "Impossible de lire l'image",
                            FaceDetected = false
                        };
                    }

                    gray = new Mat();
                    CvInvoke.CvtColor(mat, gray, ColorConversion.Bgr2Gray);

                    var faces = _faceCascade.DetectMultiScale(
                        gray,
                        _settings.ScaleFactor,
                        _settings.MinNeighbors,
                        new System.Drawing.Size(_settings.MinFaceSize, _settings.MinFaceSize));

                    if (faces.Length == 0)
                    {
                        _logger.LogInformation("Aucun visage détecté dans l'image");
                        return new FaceDetectionResult
                        {
                            Success = false,
                            Message = "Aucun visage détecté",
                            FaceDetected = false
                        };
                    }

                    _logger.LogInformation("{Count} visage(s) détecté(s)", faces.Length);

                    if (!IsModelTrained)
                    {
                        _logger.LogWarning("Tentative de reconnaissance avec un modèle non entraîné");
                        return new FaceDetectionResult
                        {
                            Success = false,
                            Message = "Modèle non entraîné. Veuillez d'abord entraîner le modèle.",
                            FaceDetected = true
                        };
                    }

                    // Prendre le plus grand visage détecté (le plus proche de la caméra)
                    var faceRect = faces.OrderByDescending(r => r.Width * r.Height).First();
                    faceMat = new Mat(gray, faceRect);
                    normalizedFace = NormalizeFace(faceMat);

                    var result = _recognizer.Predict(normalizedFace);

                    _logger.LogInformation("Reconnaissance: Label={Label}, Distance={Distance}",
                        result.Label, result.Distance);

                    if (result.Label == -1)
                    {
                        return new FaceDetectionResult
                        {
                            Success = false,
                            Message = "Visage non reconnu (hors seuil LBPH)",
                            FaceDetected = true,
                            Confidence = 0
                        };
                    }

                    // Confidence : 100% quand Distance=0, 0% quand Distance=Threshold
                    double confidence = Math.Max(0, (1.0 - result.Distance / _settings.Threshold) * 100.0);

                    if (confidence < _settings.MinConfidencePercent)
                    {
                        _logger.LogInformation("Confiance trop faible ({Confidence:F1}% < {Min}%)",
                            confidence, _settings.MinConfidencePercent);

                        return new FaceDetectionResult
                        {
                            Success = false,
                            Message = $"Visage non reconnu (confiance: {confidence:F1}%)",
                            FaceDetected = true,
                            Confidence = confidence
                        };
                    }

                    return new FaceDetectionResult
                    {
                        Success = true,
                        PersonId = result.Label,
                        FaceDetected = true,
                        Confidence = confidence,
                        Message = "Visage reconnu avec succès"
                    };
                }
                finally
                {
                    _semaphore.Release();
                }
            }
            catch (FormatException)
            {
                _logger.LogError("Format Base64 invalide");
                return new FaceDetectionResult
                {
                    Success = false,
                    Message = "Format d'image invalide (Base64)",
                    FaceDetected = false
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
            finally
            {
                mat?.Dispose();
                gray?.Dispose();
                faceMat?.Dispose();
                normalizedFace?.Dispose();
            }
        }

        public async Task TrainModelAsync()
        {
            await Task.Run(async () =>
            {
                try
                {
                    _logger.LogInformation("Début de l'entraînement du modèle...");

                    // Créer un scope pour accéder à AppDbContext (service Scoped depuis un Singleton)
                    List<PictureDirectory> pictures;
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        pictures = context.PictureDirectory
                            .Include(p => p.Person)
                            .ToList();
                    }

                    if (!pictures.Any())
                    {
                        _logger.LogWarning("Aucune image disponible pour l'entraînement");
                        return;
                    }

                    var groupedByPerson = pictures.GroupBy(p => p.PersonId);
                    _logger.LogInformation("Statistiques: {ImageCount} images, {PersonCount} personne(s)",
                        pictures.Count, groupedByPerson.Count());

                    foreach (var group in groupedByPerson)
                    {
                        var person = group.First().Person;
                        _logger.LogInformation("PersonId {Id} ({Name}): {Count} image(s)",
                            group.Key, $"{person.Firstname} {person.Lastname}", group.Count());

                        if (group.Count() < 3)
                            _logger.LogWarning("Recommandé: au moins 3-5 images par personne pour PersonId {Id}", group.Key);
                    }

                    var images = new List<Mat>();
                    var labels = new List<int>();
                    var failedImages = new List<string>();
                    var envPath = _env.WebRootPath;

                    // CascadeClassifier local pour le preprocessing (thread-safe, indépendant de _faceCascade)
                    using var trainingCascade = new CascadeClassifier(_cascadePath);

                    foreach (var pic in pictures)
                    {
                        try
                        {
                            var fullPath = Path.Combine(envPath, pic.Url.TrimStart('/'));

                            if (!File.Exists(fullPath))
                            {
                                _logger.LogWarning("Image non trouvée: {Path}", fullPath);
                                failedImages.Add(pic.Url);
                                continue;
                            }

                            var mat = CvInvoke.Imread(fullPath, ImreadModes.Grayscale);

                            if (mat.IsEmpty)
                            {
                                _logger.LogWarning("Impossible de charger l'image: {Path}", fullPath);
                                mat.Dispose();
                                failedImages.Add(pic.Url);
                                continue;
                            }

                            // CORRECTIF CRITIQUE : appliquer Haar Cascade sur les images d'entraînement
                            // pour extraire uniquement le crop du visage, identique au pipeline de prédiction.
                            // Sans ce correctif, le modèle est entraîné sur l'image entière (fond + corps)
                            // mais prédit sur un crop de visage → distances LBPH toujours > seuil → 0% de succès.
                            // TrainingMinNeighbors (3) est plus souple que MinNeighbors (5)
                            // pour accepter des photos légèrement penchées ou moins bien éclairées.
                            var detectedFaces = trainingCascade.DetectMultiScale(
                                mat,
                                _settings.ScaleFactor,
                                _settings.TrainingMinNeighbors,
                                new System.Drawing.Size(_settings.MinFaceSize, _settings.MinFaceSize));

                            if (detectedFaces.Length == 0)
                            {
                                _logger.LogWarning("Aucun visage détecté dans l'image d'entraînement: {File}. Image ignorée.", Path.GetFileName(fullPath));
                                mat.Dispose();
                                failedImages.Add(pic.Url);
                                continue;
                            }

                            // Prendre le plus grand visage détecté
                            var faceRect = detectedFaces.OrderByDescending(r => r.Width * r.Height).First();
                            using var faceMat = new Mat(mat, faceRect);
                            var normalizedMat = NormalizeFace(faceMat);

                            // Augmentation x4 : original + flip + brighter + darker
                            var variants = AugmentFace(normalizedMat);
                            normalizedMat.Dispose();
                            foreach (var v in variants)
                            {
                                images.Add(v);
                                labels.Add(pic.PersonId);
                            }

                            mat.Dispose();
                            _logger.LogDebug("Visage extrait, normalisé et augmenté x{Count}: {File}",
                                variants.Count, Path.GetFileName(fullPath));
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Erreur lors du traitement de l'image {Url}", pic.Url);
                            failedImages.Add(pic.Url);
                        }
                    }

                    if (failedImages.Any())
                        _logger.LogWarning("Echec du chargement de {FailedCount}/{TotalCount} images",
                            failedImages.Count, pictures.Count);

                    if (images.Count == 0)
                    {
                        _logger.LogError("Aucune image valide pour l'entraînement. Vérifiez que les images uploadées contiennent des visages détectables par Haar Cascade.");
                        return;
                    }

                    if (images.Count < 2)
                    {
                        _logger.LogError("Au moins 2 images valides sont nécessaires. Trouvé: {Count}", images.Count);
                        foreach (var img in images) img.Dispose();
                        return;
                    }

                    var uniqueLabels = labels.Distinct().Count();
                    if (uniqueLabels < 2)
                        _logger.LogWarning("Une seule personne détectée. Recommandé: au moins 2 personnes");

                    _logger.LogInformation("Entraînement avec {ImageCount} images pour {PersonCount} personne(s)",
                        images.Count, uniqueLabels);

                    // Sérialiser Train + Write avec le semaphore (non thread-safe)
                    await _semaphore.WaitAsync();
                    try
                    {
                        _recognizer.Train(images.ToArray(), labels.ToArray());

                        var modelDir = Path.GetDirectoryName(_modelPath);
                        if (!string.IsNullOrEmpty(modelDir))
                            Directory.CreateDirectory(modelDir);

                        _recognizer.Write(_modelPath);
                        IsModelTrained = true;
                    }
                    finally
                    {
                        _semaphore.Release();
                    }

                    _logger.LogInformation("Modèle LBPH entraîné et sauvegardé: {Path}", _modelPath);
                    foreach (var img in images) img.Dispose();
                    _logger.LogInformation("Entraînement terminé avec succès.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erreur critique lors de l'entraînement du modèle");
                    IsModelTrained = false;
                    throw;
                }
            });
        }

        public async Task<DiagnosticResult> RunDiagnosticsAsync()
        {
            var result = new DiagnosticResult();

            await Task.Run(() =>
            {
                try
                {
                    _logger.LogInformation("Exécution des diagnostics...");

                    result.ModelFileExists = File.Exists(_modelPath);
                    if (result.ModelFileExists)
                    {
                        var fileInfo = new FileInfo(_modelPath);
                        result.ModelFileSize = fileInfo.Length;
                        _logger.LogInformation("Fichier modèle: {Size} octets", result.ModelFileSize);
                    }
                    else
                    {
                        _logger.LogWarning("Fichier modèle non trouvé: {Path}", _modelPath);
                    }

                    List<PictureDirectory> pictures;
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        pictures = context.PictureDirectory
                            .Include(p => p.Person)
                            .ToList();
                    }

                    result.TotalImages = pictures.Count;
                    result.TotalPersons = pictures.Select(p => p.PersonId).Distinct().Count();

                    _logger.LogInformation("Base de données: {Images} images, {Persons} personnes",
                        result.TotalImages, result.TotalPersons);

                    var groupedByPerson = pictures.GroupBy(p => p.PersonId);
                    foreach (var group in groupedByPerson)
                    {
                        var person = group.First().Person;
                        var count = group.Count();
                        result.ImagesPerPerson[group.Key] = count;
                        _logger.LogInformation("PersonId {Id} ({Name}): {Count} image(s)",
                            group.Key, $"{person.Firstname} {person.Lastname}", count);
                    }

                    var envPath = _env.WebRootPath;
                    var existingFiles = 0;
                    var missingFiles = new List<string>();

                    foreach (var pic in pictures)
                    {
                        var fullPath = Path.Combine(envPath, pic.Url.TrimStart('/'));
                        if (File.Exists(fullPath))
                            existingFiles++;
                        else
                            missingFiles.Add(pic.Url);
                    }

                    result.ExistingImageFiles = existingFiles;
                    result.MissingImageFiles = missingFiles;

                    if (missingFiles.Any())
                    {
                        _logger.LogWarning("{Count} fichier(s) image manquant(s)", missingFiles.Count);
                        foreach (var missing in missingFiles)
                            _logger.LogWarning("Fichier manquant: {Path}", missing);
                    }
                    else
                    {
                        _logger.LogInformation("Tous les fichiers images existent");
                    }

                    result.IsModelLoaded = IsModelTrained;
                    _logger.LogInformation("Modèle chargé: {Status}", result.IsModelLoaded ? "Oui" : "Non");

                    if (result.TotalImages < 2)
                        _logger.LogWarning("RECOMMANDATION: Ajoutez au moins 2 images pour l'entraînement");

                    if (result.TotalPersons < 2)
                        _logger.LogWarning("RECOMMANDATION: Ajoutez au moins 2 personnes différentes");

                    var personsWithFewImages = result.ImagesPerPerson.Where(kvp => kvp.Value < 3);
                    if (personsWithFewImages.Any())
                        _logger.LogWarning("RECOMMANDATION: Certaines personnes ont moins de 3 images");

                    _logger.LogInformation("Diagnostics terminés");
                }
                catch (Exception ex)
                {
                    result.Error = ex.Message;
                    _logger.LogError(ex, "Erreur lors du diagnostic");
                }
            });

            return result;
        }

        public void Dispose()
        {
            try
            {
                _faceCascade?.Dispose();
                _recognizer?.Dispose();
                _semaphore?.Dispose();
                _logger.LogInformation("Ressources libérées");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la libération des ressources");
            }
        }
    }
}
