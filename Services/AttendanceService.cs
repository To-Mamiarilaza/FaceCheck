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
                _logger.LogInformation("Traitement de la présence pour PersonId: {PersonId}", personId);

                var person = await GetPersonByIdAsync(personId);
                if (person == null)
                {
                    _logger.LogWarning("Personne non trouvée avec l'Id: {PersonId}", personId);
                    return new AttendanceResult
                    {
                        Success = false,
                        Message = "Personne non trouvée"
                    };
                }

                var isAlreadyPresent = await IsAlreadyPresentTodayAsync(personId);
                if (isAlreadyPresent)
                {
                    _logger.LogInformation(
                        "{PersonName} est déjà présent aujourd'hui", 
                        $"{person.Firstname} {person.Lastname}");

                    return new AttendanceResult
                    {
                        Success = false,
                        Message = "Déjà présent aujourd'hui",
                        PersonName = $"{person.Firstname} {person.Lastname}",
                        AlreadyPresent = true
                    };
                }

                var attendance = await RecordAttendanceAsync(personId);

                _logger.LogInformation(
                    "Présence enregistrée pour {PersonName} à {Time}", 
                    $"{person.Firstname} {person.Lastname}", 
                    attendance.CheckInTime);

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
                _logger.LogError(ex, "Erreur lors du traitement de la présence pour PersonId: {PersonId}", personId);
                return new AttendanceResult
                {
                    Success = false,
                    Message = $"Erreur lors de l'enregistrement: {ex.Message}"
                };
            }
        }

        public async Task<Person?> GetPersonByIdAsync(int personId)
        {
            return await _context.Persons
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == personId);
        }

        public async Task<bool> IsAlreadyPresentTodayAsync(int personId)
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            return await _context.AttendancesRegister
                .AsNoTracking()
                .AnyAsync(a =>
                    a.PersonId == personId &&
                    a.CheckInTime >= today &&
                    a.CheckInTime < tomorrow);
        }

        public async Task<Attendance> RecordAttendanceAsync(int personId)
        {
            var attendance = new Attendance 
            { 
                PersonId = personId, 
                CheckInTime = DateTime.Now 
            };

            _context.AttendancesRegister.Add(attendance);
            await _context.SaveChangesAsync();

            return attendance;
        }
    }
}