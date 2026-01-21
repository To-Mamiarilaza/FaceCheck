
using Microsoft.EntityFrameworkCore;
using FaceCheck.Data;
using FaceCheck.Models;

namespace FaceCheck.Services
{
    public class AttendanceService : IAttendanceService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AttendanceService> _logger;

        public AttendanceService(AppDbContext context, ILogger<AttendanceService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<AttendanceResult> ProcessAttendanceAsync(int personId)
        {
            try
            {
                var person = await GetPersonByIdAsync(personId);
                if (person == null)
                {
                    return new AttendanceResult
                    {
                        Success = false,
                        Message = "Personne non trouvée"
                    };
                }

                var isAlreadyPresent = await IsAlreadyPresentTodayAsync(personId);
                if (isAlreadyPresent)
                {
                    return new AttendanceResult
                    {
                        Success = false,
                        Message = "Déjà présent aujourd'hui",
                        PersonName = $"{person.Firstname} {person. Lastname}",
                        AlreadyPresent = true
                    };
                }

                var attendance = await RecordAttendanceAsync(personId);

                return new AttendanceResult
                {
                    Success = true,
                    PersonName = $"{person.Firstname} {person.Lastname}",
                    AlreadyPresent = false,
                    CheckInTime = attendance.CheckInTime,
                    Message = "Présence enregistrée avec succès"
                };
            }
            catch (Exception ex)
            {
                _logger. LogError(ex, $"Erreur lors du traitement de la présence pour PersonId:  {personId}");
                return new AttendanceResult
                {
                    Success = false,
                    Message = $"Erreur lors de l'enregistrement:  {ex.Message}"
                };
            }
        }

        public async Task<Person?> GetPersonByIdAsync(int personId)
        {
            return await _context.Persons
                .FirstOrDefaultAsync(p => p. Id == personId);
        }

        public async Task<bool> IsAlreadyPresentTodayAsync(int personId)
        {
            var today = DateTime.Today;
            return await _context.AttendancesRegister.AnyAsync(a =>
                a.PersonId == personId &&
                a.CheckInTime >= today &&
                a.CheckInTime < today.AddDays(1));
        }

        public async Task<Attendance> RecordAttendanceAsync(int personId)
        {
            var attendance = new Attendance 
            { 
                PersonId = personId, 
                CheckInTime = DateTime.Now 
            };

            _context.AttendancesRegister.Add(attendance);
            await _context. SaveChangesAsync();

            return attendance;
        }
    }
}