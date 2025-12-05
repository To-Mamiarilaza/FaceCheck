using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FaceCheck.Pages;

public class NewEmployeeModel : PageModel
{
    private readonly ILogger<NewEmployeeModel> _logger;

    public NewEmployeeModel(ILogger<NewEmployeeModel> logger)
    {
        _logger = logger;
    }

    public void OnGet()
    {
    }
}

