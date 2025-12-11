using FaceCheck.Data;
using FaceCheck.Models;

namespace FaceCheck.Services;

public class AttendanceService
{
    private readonly AppDbContext _db;

    public AttendanceService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<bool> SaveCheckIn(int personId)
    {
        var attendance = new Attendance
        {
            PersonId = personId,
            CheckInTime = DateTime.Now
        };

        _db.Attendances.Add(attendance);
        await _db.SaveChangesAsync();
        return true;
    }
}
