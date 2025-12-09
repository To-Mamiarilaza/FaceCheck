using FaceCheck.Data;
using FaceCheck.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FaceCheck.Pages;

public class EmployeeListModel : PageModel
{
    private readonly ILogger<EmployeeListModel> _logger;
    private readonly AppDbContext _context;

    public EmployeeListModel(ILogger<EmployeeListModel> logger, AppDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    public List<Person> Persons { get; set; } = new List<Person>();
    public int TotalCount { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 5;
    public string SearchTerm { get; set; } = "";

    public void OnGet(int pageNumber = 1, string search = "")
    {
        CurrentPage = pageNumber < 1 ? 1 : pageNumber;
        SearchTerm = search?.Trim() ?? "";

        // Start with all persons
        var query = _context.Persons.AsQueryable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(SearchTerm))
        {
            var searchLower = SearchTerm.ToLower();
            query = query.Where(p => 
                p.Firstname.ToLower().Contains(searchLower) ||
                p.Lastname.ToLower().Contains(searchLower) ||
                p.Email.ToLower().Contains(searchLower)
            );
        }

        // Get total count before pagination
        TotalCount = query.Count();

        // Apply pagination
        Persons = query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((CurrentPage - 1) * PageSize)
            .Take(PageSize)
            .ToList();
    }

    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

    public IActionResult OnPost(int employeeId = 0, string action = "", int pageNumber = 1, string search = "")
    {
        if (employeeId > 0)
        {
            var person = _context.Persons.FirstOrDefault(p => p.Id == employeeId);
            if (person != null)
            {
                if (action == "delete")
                {
                    // Set status to 0 to mark as deleted
                    person.Status = 0;
                    _context.Persons.Update(person);
                    _context.SaveChanges();
                    _logger.LogInformation($"Employee {employeeId} marked as inactive");
                }
                else if (action == "reactivate")
                {
                    // Set status to 10 to reactivate
                    person.Status = 10;
                    _context.Persons.Update(person);
                    _context.SaveChanges();
                    _logger.LogInformation($"Employee {employeeId} reactivated");
                }
            }
        }

        // Redirect back to the same page with search term preserved
        if (!string.IsNullOrEmpty(search))
        {
            return RedirectToPage("/EmployeeList", new { pageNumber, search });
        }
        return RedirectToPage("/EmployeeList", new { pageNumber });
    }
}

