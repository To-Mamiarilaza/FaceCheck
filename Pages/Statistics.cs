using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FaceCheck.Pages;

public class StatisticsModel : PageModel
{
    private readonly ILogger<StatisticsModel> _logger;

    public StatisticsModel(ILogger<StatisticsModel> logger)
    {
        _logger = logger;
    }

    public void OnGet()
    {
    }
}

