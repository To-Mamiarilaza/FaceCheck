using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FaceCheck.Pages;

public class DetectionModel : PageModel
{
    private readonly ILogger<DetectionModel> _logger;

    public DetectionModel(ILogger<DetectionModel> logger)
    {
        _logger = logger;
    }

    public void OnGet()
    {
    }
}

