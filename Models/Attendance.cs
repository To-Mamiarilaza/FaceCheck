namespace FaceCheck.Models;

public class Attendance
{
    public int Id { get; set; }
    public int PersonId { get; set; }
    public DateTime CheckInTime { get; set; }

    public Person Person { get; set; }
}