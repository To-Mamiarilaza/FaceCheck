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
    }
}

