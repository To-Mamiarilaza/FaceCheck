namespace FaceCheck.Pages;

public class FaceRecognitionSettings
{
    // Confiance minimum (%) pour accepter une reconnaissance.
    // Ex: 15 = accepter dès que confidence >= 15% (distance LBPH <= 85).
    public double MinConfidencePercent { get; set; } = 15.0;

    // Seuil interne LBPH : distance max avant que le recognizer retourne Label=-1.
    public double Threshold { get; set; } = 100;

    public int MinFaceSize { get; set; } = 50;
    public double ScaleFactor { get; set; } = 1.1;

    // MinNeighbors pour la détection en temps réel (caméra) — plus strict.
    public int MinNeighbors { get; set; } = 5;

    // MinNeighbors pour l'extraction des visages lors de l'entraînement — plus souple
    // pour accepter des photos légèrement penchées ou moins bien éclairées.
    public int TrainingMinNeighbors { get; set; } = 3;

    public int LBPHRadius { get; set; } = 1;
    public int LBPHNeighbors { get; set; } = 8;
    public int LBPHGridX { get; set; } = 8;
    public int LBPHGridY { get; set; } = 8;
}
