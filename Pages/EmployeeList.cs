using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FaceCheck.Pages;

public class EmployeeListModel : PageModel
{
    private readonly ILogger<EmployeeListModel> _logger;

    public EmployeeListModel(ILogger<EmployeeListModel> logger)
    {
        _logger = logger;
    }

    public void OnGet()
    {
    }
}

