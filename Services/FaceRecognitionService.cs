using Emgu.CV;
using Emgu.CV.Structure;
namespace FaceCheck.Services;
public class FaceRecognitionService
{
    private readonly CascadeClassifier _faceCascade;

    public FaceRecognitionService()
    {
        // Fichier Haarcascade (inclus avec EmguCV)
        _faceCascade = new CascadeClassifier("haarcascade_frontalface_default.xml");
    }

    public void Detect()
    {
        using var capture = new VideoCapture(0);
        if (!capture.IsOpened)
        {
            throw new Exception("Webcam non détectée.");
        }

        using var frame = new Mat();

        while (true)
        {
            capture.Read(frame);
            if (frame.IsEmpty) continue;

            var img = frame.ToImage<Bgr, byte>();
            var gray = img.Convert<Gray, byte>();

            var faces = _faceCascade.DetectMultiScale(gray, 1.1, 5, new System.Drawing.Size(50, 50));

            foreach (var face in faces)
            {
                img.Draw(face, new Bgr(0, 255, 0), 2);
            }

            CvInvoke.Imshow("Detection Face", img);

            if (CvInvoke.WaitKey(1) == 27) break; // ESC pour quitter
        }
    }
}