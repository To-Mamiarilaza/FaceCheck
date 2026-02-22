using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using FaceCheck.Data;
using FaceCheck.Models;
using FaceCheck.Services;

namespace FaceCheck.Pages;

public class NewEmployeeModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly IFaceRecognitionService _faceRecognitionService;

    public NewEmployeeModel(AppDbContext context, IWebHostEnvironment env, IFaceRecognitionService faceRecognitionService)
    {
        _context = context;
        _env = env;
        _faceRecognitionService = faceRecognitionService;
    }

    [BindProperty]
    [Required(ErrorMessage = "Le prénom est requis.")]
    public string FirstName { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Le nom est requis.")]
    public string LastName { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "L'email est requis.")]
    [EmailAddress(ErrorMessage = "Format d'email invalide.")]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Le mot de passe est requis.")]
    [MinLength(6, ErrorMessage = "Le mot de passe doit contenir au moins 6 caractères.")]
    public string Password { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "La confirmation du mot de passe est requise.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [BindProperty]
    public List<string> FaceImagesBase64 { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            var firstError = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .FirstOrDefault();
            return BadRequest(firstError ?? "Validation échouée.");
        }

        if (FaceImagesBase64 == null || FaceImagesBase64.Count == 0)
            return BadRequest("Au moins une photo de visage est requise.");

        if (Password != ConfirmPassword)
            return BadRequest("Les mots de passe ne correspondent pas.");

        var person = new Person
        {
            Firstname = FirstName,
            Lastname = LastName,
            Email = Email,
            Password = BCrypt.Net.BCrypt.HashPassword(Password),
            Status = 1
        };

        _context.Persons.Add(person);
        await _context.SaveChangesAsync();

        var uploadsPath = Path.Combine(_env.WebRootPath, "faces");
        Directory.CreateDirectory(uploadsPath);

        foreach (var base64Image in FaceImagesBase64)
        {
            try
            {
                // Retirer le préfixe data:image/...;base64, si présent
                var base64Data = base64Image.Contains(',')
                    ? base64Image.Split(',')[1]
                    : base64Image;

                var imageBytes = Convert.FromBase64String(base64Data);
                var fileName = $"{Guid.NewGuid()}.jpg";
                var filePath = Path.Combine(uploadsPath, fileName);

                await System.IO.File.WriteAllBytesAsync(filePath, imageBytes);

                _context.PictureDirectory.Add(new PictureDirectory
                {
                    PersonId = person.Id,
                    Url = "/faces/" + fileName,
                    CreatedAt = DateTime.Now
                });
            }
            catch
            {
                // Image base64 invalide, on ignore
            }
        }

        await _context.SaveChangesAsync();

        // Réentraînement automatique du modèle en arrière-plan
        _ = _faceRecognitionService.TrainModelAsync();

        return new OkResult();
    }
}
