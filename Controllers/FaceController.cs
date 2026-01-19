
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Emgu.CV;
using Emgu.CV.Face;
using FaceCheck.Data;
using FaceCheck.Models; // Pour EigenFaceRecognizer / LBPHFaceRecognizer

namespace FaceCheck.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FaceController : ControllerBase
    {
        private readonly CascadeClassifier _faceCascade;
        private readonly AppDbContext _context;
        private readonly LBPHFaceRecognizer _recognizer;
        private readonly IWebHostEnvironment _env;
        private readonly string _modelPath;

        public FaceController(AppDbContext context, IWebHostEnvironment env)
        {
            _faceCascade = new CascadeClassifier(
                "/home/zaby/M2/FaceCheck/haarcascade_frontalface_default.xml");

            _context = context;
            _env = env;

            _recognizer = new LBPHFaceRecognizer(1, 8, 8, 8, 100);

            _modelPath = Path.Combine(_env.ContentRootPath, "Models", "face_model.yml");

            if (System.IO.File.Exists(_modelPath))
            {
                // 🔹 Charger le modèle déjà entraîné
                _recognizer.Read(_modelPath);
                Console.WriteLine("✔ Modèle LBPH chargé");
            }
            else
            {
                // 🔹 Entraîner UNE SEULE FOIS
                TrainRecognizer();

                Directory.CreateDirectory(Path.GetDirectoryName(_modelPath)!);
                _recognizer.Write(_modelPath);

                Console.WriteLine("✔ Modèle LBPH entraîné et sauvegardé");
            }
        }


        // Classe pour la requête frontend
        public class FaceDetectRequest
        {
            public string ImageBase64 { get; set; }
        }

        // API POST /api/face/detect
        [HttpPost("detect")]
        public IActionResult Detect([FromBody] FaceDetectRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ImageBase64))
                return BadRequest(new { success = false, message = "Image vide" });

            try
            {
                // Décodage Base64 en tableau de bytes
                byte[] imageBytes = Convert.FromBase64String(request.ImageBase64);

                // Décodage en Mat
                Mat mat = new Mat();
                CvInvoke.Imdecode(imageBytes, Emgu.CV.CvEnum.ImreadModes.ColorRgb, mat);

                if (mat.IsEmpty)
                    return BadRequest(new { success = false, message = "Impossible de lire l'image" });

                // Conversion en gris
                using var gray = new Mat();
                CvInvoke.CvtColor(mat, gray, Emgu.CV.CvEnum.ColorConversion.Bgr2Gray);

                // Détection des visages
                var faces = _faceCascade.DetectMultiScale(gray, 1.1, 5, new System.Drawing.Size(50, 50));
                if (faces.Length == 0)
                    return Ok(new { success = false, message = "Aucun visage détecté" });

                var faceRect = faces[0];
                var faceMat = new Mat(gray, faceRect); // Recadrage du visage

                // Reconnaissance
                var result = _recognizer.Predict(faceMat);

                if (result.Label == -1 || result.Distance > 80) // seuil à ajuster
                    return Ok(new { success = false, message = "Visage non reconnu" });

                // Récupération de la personne dans la DB
                var person = _context.Persons.FirstOrDefault(p => p.Id == result.Label);
                if (person == null)
                    return Ok(new { success = false, message = "Personne non trouvée" });

                // Vérifier si déjà présent aujourd'hui
                var today = DateTime.Today;
                bool alreadyPresentToday = _context.AttendancesRegister.Any(a =>
                    a.PersonId == person.Id &&
                    a.CheckInTime >= today &&
                    a.CheckInTime < today.AddDays(1)
                );

                if (alreadyPresentToday)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "Déjà présent aujourd'hui",
                        name = $"{person.Firstname} {person.Lastname}"
                    });
                }
                // Insertion présence
                var attendance = new Attendance { PersonId = person.Id, CheckInTime = DateTime.Now };
                _context.AttendancesRegister.Add(attendance);
                _context.SaveChanges();

                return Ok(new
                {
                    success = true,
                    name = $"{person.Firstname} {person.Lastname}",
                    confidence = 100 - result.Distance // ou ajuster selon ton algorithme
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Erreur traitement image : {ex.Message}" });
            }
        }

        // Entraîne le recognizer avec les images de la DB
        private void TrainRecognizer()
        {
            var pictures = _context.PictureDirectory
                .Include(p => p.Person)
                .ToList();

            if (!pictures.Any()) return;

            var images = new List<Mat>();
            var labels = new List<int>();

            var envPath = _env.WebRootPath;

            foreach (var pic in pictures)
            {
                try
                {
                    var fullPath = Path.Combine(envPath, pic.Url.TrimStart('/'));

                    if (!System.IO.File.Exists(fullPath)) continue;

                    var mat = CvInvoke.Imread(fullPath, Emgu.CV.CvEnum.ImreadModes.Grayscale);

                    if (!mat.IsEmpty)
                    {
                        images.Add(mat);
                        labels.Add(pic.Person.Id);
                    }
                }
                catch { }
            }

            if (images.Count > 0)
                _recognizer.Train(images.ToArray(), labels.ToArray());
        }

    }


}
