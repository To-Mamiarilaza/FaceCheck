using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Threading.Tasks;
using FaceCheck.Data;
using FaceCheck.Models;

namespace FaceCheck.Pages;

public class NewEmployeeModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _env;

    public NewEmployeeModel(AppDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    [BindProperty]
    public string FirstName { get; set; }

    [BindProperty]
    public string LastName { get; set; }

    [BindProperty]
    public string Email { get; set; }

    [BindProperty]
    public IFormFile FaceFile { get; set; } // fichier uploadé

    public void OnGet()
    {
        // initialisation si nécessaire
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page(); // retourne la page si des validations échouent
        }

        // 1️⃣ Créer l'employé
        var person = new Person
        {
            Firstname = FirstName,
            Lastname = LastName,
            Email = Email,
            Password = "default" // tu peux générer un hash
        };

        _context.Persons.Add(person);
        await _context.SaveChangesAsync(); // persiste en DB pour obtenir l'ID

        // 2️⃣ Sauvegarder la photo
        if (FaceFile != null)
        {
            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads");
            Directory.CreateDirectory(uploadsFolder);

            var fileName = $"{person.Id}_{Path.GetFileName(FaceFile.FileName)}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await FaceFile.CopyToAsync(stream);
            }

            var picture = new PictureDirectory
            {
                PersonId = person.Id,
                Url = $"/uploads/{fileName}"
            };

            _context.PictureDirectory.Add(picture);
            await _context.SaveChangesAsync();
        }

        // 3️⃣ Redirection ou message de succès
        return RedirectToPage("/Index"); // retour à l'accueil
    }
    }


