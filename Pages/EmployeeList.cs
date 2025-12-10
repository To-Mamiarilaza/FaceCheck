using FaceCheck.Data;
using FaceCheck.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text;

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

    public IActionResult OnPostExport()
    {
        try
        {
            // Get all active employees from database
            var employees = _context.Persons
                .OrderByDescending(p => p.CreatedAt)
                .ToList();

            // Build CSV content
            var csv = new StringBuilder();
            
            // Add CSV header
            csv.AppendLine("FirstName,LastName,Email,Status,CreatedAt");

            // Add employee rows
            foreach (var employee in employees)
            {
                var statusText = employee.Status == 10 ? "Active" : "Inactive";
                var createdAtFormatted = employee.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");
                
                // Escape CSV values (add quotes if contains comma, quotes, or newlines)
                var firstName = EscapeCsvValue(employee.Firstname);
                var lastName = EscapeCsvValue(employee.Lastname);
                var email = EscapeCsvValue(employee.Email);
                
                csv.AppendLine($"{firstName},{lastName},{email},{statusText},{createdAtFormatted}");
            }

            // Convert to byte array
            byte[] buffer = Encoding.UTF8.GetBytes(csv.ToString());
            
            // Set response headers
            var fileName = $"employees_export_{DateTime.Now:yyyy-MM-dd_HHmmss}.csv";
            Response.ContentType = "text/csv";
            Response.Headers["Content-Disposition"] = $"attachment; filename=\"{fileName}\"";
            
            return File(buffer, "text/csv", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error exporting employees: {ex.Message}");
            return StatusCode(500, "An error occurred while exporting employees");
        }
    }

    private string EscapeCsvValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        
        // If value contains comma, quote, or newline, wrap in quotes and escape internal quotes
        if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        
        return value;
    }

    [BindProperty]
    public IFormFile? ImportFile { get; set; }

    public async Task<IActionResult> OnPostImport()
    {
        try
        {
            if (ImportFile == null || ImportFile.Length == 0)
            {
                return BadRequest("No file selected");
            }

            // Validate file extension
            var fileName = Path.GetFileName(ImportFile.FileName);
            if (!fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Only CSV files are supported");
            }

            var importedCount = 0;
            var skippedCount = 0;
            var errors = new List<string>();

            using (var stream = new StreamReader(ImportFile.OpenReadStream()))
            {
                var headerLine = await stream.ReadLineAsync();
                if (string.IsNullOrEmpty(headerLine))
                {
                    return BadRequest("CSV file is empty");
                }

                // Validate header
                var expectedHeader = "FirstName,LastName,Email,Status,CreatedAt";
                if (headerLine.Trim() != expectedHeader)
                {
                    return BadRequest($"Invalid CSV header. Expected: {expectedHeader}");
                }

                var lineNumber = 2;
                string? line;
                while ((line = await stream.ReadLineAsync()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        lineNumber++;
                        continue;
                    }

                    try
                    {
                        var values = ParseCsvLine(line);
                        
                        if (values.Count < 5)
                        {
                            errors.Add($"Line {lineNumber}: Invalid number of columns");
                            skippedCount++;
                            lineNumber++;
                            continue;
                        }

                        var firstName = values[0].Trim();
                        var lastName = values[1].Trim();
                        var email = values[2].Trim();
                        var statusText = values[3].Trim();
                        var createdAtText = values[4].Trim();

                        // Validate required fields
                        if (string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName) || string.IsNullOrEmpty(email))
                        {
                            errors.Add($"Line {lineNumber}: Missing required fields (FirstName, LastName, Email)");
                            skippedCount++;
                            lineNumber++;
                            continue;
                        }

                        // Validate email format
                        if (!email.Contains("@"))
                        {
                            errors.Add($"Line {lineNumber}: Invalid email format");
                            skippedCount++;
                            lineNumber++;
                            continue;
                        }

                        // Parse status
                        var status = statusText.Equals("Active", StringComparison.OrdinalIgnoreCase) ? 10 : 0;

                        // Parse created date
                        if (!DateTime.TryParse(createdAtText, out var createdAt))
                        {
                            createdAt = DateTime.UtcNow;
                        }

                        // Check if employee already exists (by email)
                        var existingEmployee = _context.Persons.FirstOrDefault(p => p.Email == email);
                        if (existingEmployee != null)
                        {
                            errors.Add($"Line {lineNumber}: Employee with email '{email}' already exists");
                            skippedCount++;
                            lineNumber++;
                            continue;
                        }

                        // Create new person
                        var person = new Person
                        {
                            Firstname = firstName,
                            Lastname = lastName,
                            Email = email,
                            Status = status,
                            CreatedAt = createdAt
                        };

                        _context.Persons.Add(person);
                        importedCount++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Line {lineNumber}: {ex.Message}");
                        skippedCount++;
                    }

                    lineNumber++;
                }
            }

            // Save all changes to database
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Imported {importedCount} employees, skipped {skippedCount}");

            // Store import results in TempData for display
            TempData["ImportMessage"] = $"Successfully imported {importedCount} employees. {(skippedCount > 0 ? $"Skipped {skippedCount} records." : "")}";
            if (errors.Count > 0)
            {
                TempData["ImportErrors"] = string.Join(" | ", errors.Take(10)); // Show first 10 errors
            }

            return RedirectToPage("/EmployeeList");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error importing employees: {ex.Message}");
            TempData["ImportError"] = "An error occurred while importing the file";
            return RedirectToPage("/EmployeeList");
        }
    }

    private List<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var currentValue = new StringBuilder();
        var inQuotes = false;
        var i = 0;

        while (i < line.Length)
        {
            var ch = line[i];

            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    // Escaped quote
                    currentValue.Append('"');
                    i += 2;
                }
                else
                {
                    // Toggle quote mode
                    inQuotes = !inQuotes;
                    i++;
                }
            }
            else if (ch == ',' && !inQuotes)
            {
                // End of field
                values.Add(currentValue.ToString());
                currentValue.Clear();
                i++;
            }
            else
            {
                currentValue.Append(ch);
                i++;
            }
        }

        // Add the last field
        values.Add(currentValue.ToString());

        return values;
    }
}

