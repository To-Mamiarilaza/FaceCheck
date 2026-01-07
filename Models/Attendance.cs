using System.ComponentModel.DataAnnotations.Schema;

namespace FaceCheck.Models;
[Table("Attendances", Schema = "dbo")]
public class Attendance
{
    public int Id { get; set; }
    public int PersonId { get; set; }
    public DateTime CheckInTime { get; set; }

    public Person Person { get; set; }
}