namespace FaceCheck.Models
{
    public class AttendanceReport
    {
        public DateTime DayDate { get; set; }
        public string DayName { get; set; }
        public int IsWeekend { get; set; }
        public int PersonId { get; set; }
        public string Firstname { get; set; }
        public string Lastname { get; set; }
        public int Attendance { get; set; }
    }
}