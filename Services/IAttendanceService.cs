using FaceCheck.Models;

namespace FaceCheck.Services
{
    public interface IAttendanceService
    {
        Task<AttendanceResult> ProcessAttendanceAsync(int personId);
        Task<Person?> GetPersonByIdAsync(int personId);
        Task<bool> IsAlreadyPresentTodayAsync(int personId);
        Task<Attendance> RecordAttendanceAsync(int personId);
    }

    public class AttendanceResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string? PersonName { get; set; }
        public bool AlreadyPresent { get; set; }
        public DateTime? CheckInTime { get; set; }
    }
}