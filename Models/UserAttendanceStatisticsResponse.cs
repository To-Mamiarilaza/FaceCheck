namespace FaceCheck.Models
{
    public class UserAttendanceStatisticsResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public UserStatisticsData? Data { get; set; }
    }

    public class UserStatisticsData
    {
        public int UserId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        
        // Current month statistics
        public int Year { get; set; }
        public int Month { get; set; }
        public int WorkingDaysInMonth { get; set; }
        public int DaysPresent { get; set; }
        public int DaysAbsent { get; set; }
        public decimal AttendancePercentage { get; set; }
        public decimal AbsenceRate { get; set; }
        
        // Daily records
        public List<DailyAttendance> DailyRecords { get; set; } = [];
    }

    public class DailyAttendance
    {
        public DateTime Date { get; set; }
        public string DayName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // "Present", "Absent", "Weekend"
        public bool IsWeekend { get; set; }
    }
}
