using FaceCheck.Data;
using FaceCheck.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace FaceCheck.Pages;

public class StatisticsModel : PageModel
{
    private readonly ILogger<StatisticsModel> _logger;
    private readonly AppDbContext _context;

    public StatisticsModel(ILogger<StatisticsModel> logger, AppDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    public List<MonthDay> Days { get; set; } = [];
    public List<AttendanceReport> Attendances { get; set; } = [];
    public List<AbsenceRate> AbsenceRates { get; set; } = [];
    
    public int TotalEmployees { get; set; }
    public int TodayPresent { get; set; }
    public int TodayTotal { get; set; }
    public int TodayAbsent { get; set; }
    public decimal MonthAveragePresence { get; set; }
    
    [BindProperty(SupportsGet = true)]
    public string? Month { get; set; }
    
    [BindProperty(SupportsGet = true)]
    public string? SearchEmployee { get; set; }

    public void OnGet()
    {
        // Parse month parameter or use current date
        DateTime selectedDate = DateTime.Now;
        
        if (!string.IsNullOrEmpty(Month) && DateTime.TryParse($"{Month}-01", out var parsedDate))
        {
            selectedDate = parsedDate;
        }

        int year = selectedDate.Year;
        int month = selectedDate.Month;

        // Get all days in the month
        Days = _context.GetMonthDays(year, month).ToList();
        
        // Get attendance data
        Attendances = _context.GetMonthlyAttendanceReport(year, month).ToList();

        // Filter by employee name if search parameter provided
        if (!string.IsNullOrEmpty(SearchEmployee))
        {
            var searchTerm = SearchEmployee.ToLower();
            Attendances = Attendances
                .Where(a => $"{a.Firstname} {a.Lastname}".ToLower().Contains(searchTerm))
                .ToList();
        }

        // Get yearly absence rates
        AbsenceRates = _context.GetYearlyAbsenceRates(year).ToList();

        // Calculate statistics
        CalculateStats(year, month);
    }

    private void CalculateStats(int year, int month)
    {
        // Get total unique employees from attendance data
        TotalEmployees = Attendances.Select(a => a.PersonId).Distinct().Count();

        // Get today's attendance
        DateTime today = DateTime.Now;
        int todayYear = today.Year;
        int todayMonth = today.Month;
        int todayDay = today.Day;

        if (year == todayYear && month == todayMonth)
        {
            // We're viewing the current month, so we can show today's stats
            var todayAttendance = Attendances
                .Where(a => a.DayDate.Day == todayDay)
                .ToList();

            TodayTotal = todayAttendance.Select(a => a.PersonId).Distinct().Count();
            TodayPresent = todayAttendance.Count(a => a.Attendance == 1);
            TodayAbsent = TodayTotal - TodayPresent;
        }
        else
        {
            // Not viewing current month, so no today's data
            TodayTotal = 0;
            TodayPresent = 0;
            TodayAbsent = 0;
        }

        // Calculate month average presence
        if (TotalEmployees > 0 && Attendances.Any())
        {
            // Count working days (non-weekend days) in the month
            var workingDays = Days.Count(d => d.DayDate.DayOfWeek != DayOfWeek.Saturday && d.DayDate.DayOfWeek != DayOfWeek.Sunday);
            
            if (workingDays > 0)
            {
                // Count total presence records for the month
                int totalPresenceRecords = Attendances.Count(a => a.Attendance == 1 && a.IsWeekend == 0);
                int totalExpectedRecords = TotalEmployees * workingDays;
                
                MonthAveragePresence = totalExpectedRecords > 0 
                    ? Math.Round((decimal)totalPresenceRecords / totalExpectedRecords * 100, 2)
                    : 0;
            }
        }
    }
}

