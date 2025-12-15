using FaceCheck.Models;
using System.Data;
using Microsoft.Data.SqlClient;

namespace FaceCheck.Services
{
    public interface IStatisticsService
    {
        Task<UserAttendanceStatisticsResponse> GetUserAttendanceStatisticsAsync(int userId);
        Task<UserAttendanceStatisticsResponse> GetUserAttendanceStatisticsAsync(int userId, int year, int month);
    }

    public class StatisticsService : IStatisticsService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<StatisticsService> _logger;

        public StatisticsService(IConfiguration configuration, ILogger<StatisticsService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<UserAttendanceStatisticsResponse> GetUserAttendanceStatisticsAsync(int userId)
        {
            var today = DateTime.Now;
            return await GetUserAttendanceStatisticsAsync(userId, today.Year, today.Month);
        }

        public async Task<UserAttendanceStatisticsResponse> GetUserAttendanceStatisticsAsync(int userId, int year, int month)
        {
            try
            {
                // Get user data
                var user = await GetUserByIdAsync(userId);
                if (user == null)
                {
                    return new UserAttendanceStatisticsResponse
                    {
                        Success = false,
                        Message = "User not found."
                    };
                }

                // Get attendance data for the month
                var (dailyRecords, daysPresent, daysAbsent, workingDays) = 
                    await GetMonthlyAttendanceDataAsync(userId, year, month);

                // Calculate absence rate
                var absenceRate = workingDays > 0 
                    ? Math.Round((decimal)daysAbsent / workingDays * 100, 2) 
                    : 0;

                var attendancePercentage = workingDays > 0
                    ? Math.Round((decimal)daysPresent / workingDays * 100, 2)
                    : 0;

                var data = new UserStatisticsData
                {
                    UserId = user.Id,
                    FirstName = user.Firstname,
                    LastName = user.Lastname,
                    Email = user.Email,
                    Year = year,
                    Month = month,
                    WorkingDaysInMonth = workingDays,
                    DaysPresent = daysPresent,
                    DaysAbsent = daysAbsent,
                    AttendancePercentage = attendancePercentage,
                    AbsenceRate = absenceRate,
                    DailyRecords = dailyRecords
                };

                return new UserAttendanceStatisticsResponse
                {
                    Success = true,
                    Message = "Statistics retrieved successfully.",
                    Data = data
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting user attendance statistics: {ex.Message}");
                return new UserAttendanceStatisticsResponse
                {
                    Success = false,
                    Message = "An error occurred while retrieving statistics."
                };
            }
        }

        private async Task<Person?> GetUserByIdAsync(int userId)
        {
            const string query = @"
                SELECT Id, Firstname, Lastname, Email, Password, Status, CreatedAt
                FROM Persons
                WHERE Id = @UserId
            ";

            using (var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                await connection.OpenAsync();

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@UserId", userId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return new Person
                            {
                                Id = reader.GetInt32(0),
                                Firstname = reader.GetString(1),
                                Lastname = reader.GetString(2),
                                Email = reader.GetString(3),
                                Password = reader.GetString(4),
                                Status = reader.GetInt32(5),
                                CreatedAt = reader.GetDateTime(6)
                            };
                        }
                    }
                }
            }

            return null;
        }

        private async Task<(List<DailyAttendance>, int, int, int)> GetMonthlyAttendanceDataAsync(int userId, int year, int month)
        {
            var dailyRecords = new List<DailyAttendance>();
            int daysPresent = 0;
            int daysAbsent = 0;
            int workingDays = 0;

            const string query = @"
                SELECT 
                    md.DayDate,
                    md.DayName,
                    md.IsWeekend,
                    CASE 
                        WHEN a.PersonId IS NOT NULL THEN 1
                        WHEN p.CreatedAt > md.DayDate THEN -1
                        ELSE 0
                    END AS Attendance
                FROM
                    GetMonthDays(@Year, @Month) AS md
                CROSS JOIN 
                    Persons AS p
                LEFT JOIN 
                    Attendances AS a
                        ON a.PersonId = p.Id
                        AND CAST(a.CheckInTime AS DATE) = md.DayDate
                WHERE p.Id = @UserId
                ORDER BY md.DayDate
            ";

            using (var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                await connection.OpenAsync();

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@UserId", userId);
                    command.Parameters.AddWithValue("@Year", year);
                    command.Parameters.AddWithValue("@Month", month);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var dayDate = reader.GetDateTime(0);
                            var dayName = reader.GetString(1);
                            var isWeekend = reader.GetInt32(2) == 1;
                            var attendance = reader.GetInt32(3);

                            string status = "Absent";
                            if (isWeekend)
                            {
                                status = "Weekend";
                            }
                            else if (attendance == 1)
                            {
                                status = "Present";
                                daysPresent++;
                                workingDays++;
                            }
                            else if (attendance == 0)
                            {
                                daysAbsent++;
                                workingDays++;
                            }
                            else if (attendance == -1)
                            {
                                status = "Not created yet";
                            }

                            dailyRecords.Add(new DailyAttendance
                            {
                                Date = dayDate,
                                DayName = dayName,
                                Status = status,
                                IsWeekend = isWeekend
                            });
                        }
                    }
                }
            }

            return (dailyRecords, daysPresent, daysAbsent, workingDays);
        }
    }
}
