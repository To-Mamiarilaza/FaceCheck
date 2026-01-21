namespace FaceCheck.Pages;

public class FaceRecognitionSettings
{
    public double ConfidenceThreshold { get; set; } = 80;
    public double Threshold { get; set; } = 100;
    public int MinFaceSize { get; set; } = 50;
    public double ScaleFactor { get; set; } = 1.1;
    public int MinNeighbors { get; set; } = 5;
    public int LBPHRadius { get; set; } = 1;
    public int LBPHNeighbors { get; set; } = 8;
    public int LBPHGridX { get; set; } = 8;
    public int LBPHGridY { get; set; } = 8;
}