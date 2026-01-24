using Microsoft.EntityFrameworkCore;
using Emgu.CV;
using Emgu.CV.Face;
using Emgu.CV.CvEnum;
using FaceCheck.Data;
using FaceCheck.Models;

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

        // Paramètres de configuration
        private const double SCALE_FACTOR = 1.1;
        private const int MIN_NEIGHBORS = 5;
        private const int MIN_FACE_SIZE = 50;
        private const double CONFIDENCE_THRESHOLD = 80.0;
        private const int STANDARD_FACE_WIDTH = 100;
        private const int STANDARD_FACE_HEIGHT = 100;

        // Paramètres LBPH
        private const int LBPH_RADIUS = 1;
        private const int LBPH_NEIGHBORS = 8;
        private const int LBPH_GRID_X = 8;
        private const int LBPH_GRID_Y = 8;
        private const double LBPH_THRESHOLD = 100.0;

        public bool IsModelTrained { get; private set; }

        public FaceRecognitionService(
            AppDbContext context,
            IWebHostEnvironment env,
            ILogger<FaceRecognitionService> logger)
        {
            _logger = logger;
            _context = context;
            _env = env;

            // Configuration du chemin de cascade
            _cascadePath = Path.Combine(env.ContentRootPath, "haarcascade_frontalface_default.xml");

            if (!File.Exists(_cascadePath))
            {
                _logger.LogError("Cascade file not found at: {Path}", _cascadePath);
                throw new FileNotFoundException($"Cascade file not found at: {_cascadePath}");
            }

            _faceCascade = new CascadeClassifier(_cascadePath);

            // Initialisation du recognizer LBPH
            _recognizer = new LBPHFaceRecognizer(
                LBPH_RADIUS,
                LBPH_NEIGHBORS,
                LBPH_GRID_X,
                LBPH_GRID_Y,
                LBPH_THRESHOLD);

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

                    // Vérification de l'intégrité du modèle
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
                // Test simple pour vérifier que le modèle peut faire des prédictions
                var testMat = new Mat(STANDARD_FACE_HEIGHT, STANDARD_FACE_WIDTH, DepthType.Cv8U, 1);
                testMat.SetTo(new Emgu.CV.Structure.MCvScalar(128)); // Remplir avec du gris

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
        /// Normalise un visage : redimensionnement et égalisation d'histogramme
        /// </summary>
        private Mat NormalizeFace(Mat faceMat)
        {
            var normalized = new Mat();

            // 1. Redimensionner à une taille standard
            CvInvoke.Resize(faceMat, normalized, 
                new System.Drawing.Size(STANDARD_FACE_WIDTH, STANDARD_FACE_HEIGHT));

            // 2. Égalisation d'histogramme pour normaliser l'éclairage
            CvInvoke.EqualizeHist(normalized, normalized);

            return normalized;
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
                // Décodage Base64
                byte[] imageBytes = Convert.FromBase64String(imageBase64);

// Dossier de sauvegarde
                var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "captures");
                Directory.CreateDirectory(uploadsPath);

// Nom du fichier
                var fileName = $"{DateTime.Now:yyyy-MM-dd-HH-mm-ss}-{Guid.NewGuid()}.jpg";
                var filePath = Path.Combine(uploadsPath, fileName);

// Écriture du fichier
                await System.IO.File.WriteAllBytesAsync(filePath, imageBytes);
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

                // Conversion en niveaux de gris
                gray = new Mat();
                CvInvoke.CvtColor(mat, gray, ColorConversion.Bgr2Gray);

                // Détection des visages
                var faces = _faceCascade.DetectMultiScale(
                    gray,
                    SCALE_FACTOR,
                    MIN_NEIGHBORS,
                    new System.Drawing.Size(MIN_FACE_SIZE, MIN_FACE_SIZE));

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

                // Reconnaissance du premier visage détecté
                var faceRect = faces[0];
                faceMat = new Mat(gray, faceRect);

                // NORMALISATION DU VISAGE (CRITIQUE!)
                normalizedFace = NormalizeFace(faceMat);

                // Prédiction
                var result = _recognizer.Predict(normalizedFace);

                _logger.LogInformation(
                    "Reconnaissance: Label={Label}, Distance={Distance}",
                    result.Label,
                    result.Distance);

                // Vérification du résultat
                if (result.Label == -1)
                {
                    return new FaceDetectionResult
                    {
                        Success = false,
                        Message = "Visage non reconnu (Label=-1)",
                        FaceDetected = true,
                        Confidence = 0
                    };
                }

                if (result.Distance > CONFIDENCE_THRESHOLD)
                {
                    _logger.LogInformation(
                        "Distance trop grande ({Distance} > {Threshold})",
                        result.Distance,
                        CONFIDENCE_THRESHOLD);

                    return new FaceDetectionResult
                    {
                        Success = false,
                        Message = $"Visage non reconnu (confiance trop faible: {100 - result.Distance:F2}%)",
                        FaceDetected = true,
                        Confidence = 100 - result.Distance
                    };
                }

                // Succès!
                return new FaceDetectionResult
                {
                    Success = true,
                    PersonId = result.Label,
                    FaceDetected = true,
                    Confidence = 100 - result.Distance,
                    Message = "Visage reconnu avec succès"
                };
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
                // Libération de la mémoire
                mat?.Dispose();
                gray?.Dispose();
                faceMat?.Dispose();
                normalizedFace?.Dispose();
            }
        }

        public async Task TrainModelAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    _logger.LogInformation("Début de l'entraînement du modèle...");

                    var pictures = _context.PictureDirectory
                        .Include(p => p.Person)
                        .ToList();

                    if (!pictures.Any())
                    {
                        _logger.LogWarning("Aucune image disponible pour l'entraînement");
                        return;
                    }

                    // Validation : au moins 2 images
                    if (pictures.Count < 2)
                    {
                        _logger.LogError("Pas assez d'images pour l'entraînement. Minimum: 2, Trouvé: {Count}", pictures.Count);
                        return;
                    }

                    // Statistiques par personne
                    var groupedByPerson = pictures.GroupBy(p => p.PersonId);
                    _logger.LogInformation("📊 Statistiques:");
                    _logger.LogInformation("   - Total images: {Count}", pictures.Count);
                    _logger.LogInformation("   - Total personnes: {Count}", groupedByPerson.Count());

                    foreach (var group in groupedByPerson)
                    {
                        var person = group.First().Person;
                        _logger.LogInformation("   - {Name}: {Count} image(s)",
                            $"{person.Firstname} {person.Lastname}",
                            group.Count());

                        if (group.Count() < 3)
                        {
                            _logger.LogWarning("     Recommandé: au moins 3-5 images par personne");
                        }
                    }

                    var images = new List<Mat>();
                    var labels = new List<int>();
                    var failedImages = new List<string>();
                    var envPath = _env.WebRootPath;

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
                                failedImages.Add(pic.Url);
                                continue;
                            }

                            // NORMALISATION DES IMAGES D'ENTRAÎNEMENT
                            var normalizedMat = NormalizeFace(mat);
                            images.Add(normalizedMat);
                            labels.Add(pic.PersonId);

                            mat.Dispose(); // Libérer l'image originale

                            _logger.LogDebug("Image chargée et normalisée: {Path}", Path.GetFileName(fullPath));
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Erreur lors du traitement de l'image {Url}", pic.Url);
                            failedImages.Add(pic.Url);
                        }
                    }

                    if (failedImages.Any())
                    {
                        _logger.LogWarning(" Échec du chargement de {Count}/{Total} images",
                            failedImages.Count,
                            pictures.Count);
                    }

                    if (images.Count == 0)
                    {
                        _logger.LogError(" Aucune image valide pour l'entraînement");
                        return;
                    }

                    if (images.Count < 2)
                    {
                        _logger.LogError(" Au moins 2 images valides sont nécessaires. Trouvé: {Count}", images.Count);
                        
                        // Libération de la mémoire
                        foreach (var img in images)
                        {
                            img.Dispose();
                        }
                        return;
                    }

                    // Vérification: au moins 2 personnes différentes
                    var uniqueLabels = labels.Distinct().Count();
                    if (uniqueLabels < 2)
                    {
                        _logger.LogWarning(" Une seule personne détectée. Recommandé: au moins 2 personnes");
                    }

                    _logger.LogInformation(" Entraînement avec {ImageCount} images pour {PersonCount} personne(s)",
                        images.Count,
                        uniqueLabels);

                    // Entraînement du modèle
                    _recognizer.Train(images.ToArray(), labels.ToArray());

                    // Sauvegarde du modèle
                    var modelDir = Path.GetDirectoryName(_modelPath);
                    if (!string.IsNullOrEmpty(modelDir))
                    {
                        Directory.CreateDirectory(modelDir);
                    }

                    _recognizer.Write(_modelPath);
                    IsModelTrained = true;

                    _logger.LogInformation("Modèle LBPH entraîné et sauvegardé: {Path}", _modelPath);

                    // Libération de la mémoire
                    foreach (var img in images)
                    {
                        img.Dispose();
                    }

                    _logger.LogInformation(" Entraînement terminé avec succès!");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, " Erreur critique lors de l'entraînement du modèle");
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

                    // 1. Vérifier le fichier de modèle
                    result.ModelFileExists = File.Exists(_modelPath);
                    if (result.ModelFileExists)
                    {
                        var fileInfo = new FileInfo(_modelPath);
                        result.ModelFileSize = fileInfo.Length;
                        _logger.LogInformation(" Fichier modèle: {Size} octets", result.ModelFileSize);
                    }
                    else
                    {
                        _logger.LogWarning("Fichier modèle non trouvé: {Path}", _modelPath);
                    }

                    // 2. Vérifier les images en base
                    var pictures = _context.PictureDirectory
                        .Include(p => p.Person)
                        .ToList();

                    result.TotalImages = pictures.Count;
                    result.TotalPersons = pictures.Select(p => p.PersonId).Distinct().Count();

                    _logger.LogInformation("Base de données: {Images} images, {Persons} personnes",
                        result.TotalImages,
                        result.TotalPersons);

                    // 3. Vérifier les images par personne
                    var groupedByPerson = pictures.GroupBy(p => p.PersonId);
                    foreach (var group in groupedByPerson)
                    {
                        var person = group.First().Person;
                        var count = group.Count();
                        result.ImagesPerPerson[group.Key] = count;

                        _logger.LogInformation("  - PersonId {Id} ({Name}): {Count} image(s)",
                            group.Key,
                            $"{person.Firstname} {person.Lastname}",
                            count);
                    }

                    // 4. Vérifier que les fichiers existent physiquement
                    var envPath = _env.WebRootPath;
                    var existingFiles = 0;
                    var missingFiles = new List<string>();

                    foreach (var pic in pictures)
                    {
                        var fullPath = Path.Combine(envPath, pic.Url.TrimStart('/'));
                        if (File.Exists(fullPath))
                        {
                            existingFiles++;
                        }
                        else
                        {
                            missingFiles.Add(pic.Url);
                        }
                    }

                    result.ExistingImageFiles = existingFiles;
                    result.MissingImageFiles = missingFiles;

                    if (missingFiles.Any())
                    {
                        _logger.LogWarning("{Count} fichier(s) image manquant(s)", missingFiles.Count);
                        foreach (var missing in missingFiles)
                        {
                            _logger.LogWarning("  - {Path}", missing);
                        }
                    }
                    else
                    {
                        _logger.LogInformation("Tous les fichiers images existent");
                    }

                    // 5. Vérifier l'état du recognizer
                    result.IsModelLoaded = IsModelTrained;
                    _logger.LogInformation("Modèle chargé: {Status}", result.IsModelLoaded ? "Oui" : "Non");

                    // 6. Recommandations
                    if (result.TotalImages < 2)
                    {
                        _logger.LogWarning("RECOMMANDATION: Ajoutez au moins 2 images pour l'entraînement");
                    }

                    if (result.TotalPersons < 2)
                    {
                        _logger.LogWarning("RECOMMANDATION: Ajoutez au moins 2 personnes différentes");
                    }

                    var personsWithFewImages = result.ImagesPerPerson.Where(kvp => kvp.Value < 3);
                    if (personsWithFewImages.Any())
                    {
                        _logger.LogWarning("RECOMMANDATION: Certaines personnes ont moins de 3 images");
                    }

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
                _logger.LogInformation("Ressources libérées");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la libération des ressources");
            }
        }
    }
}