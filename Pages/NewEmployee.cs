using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
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
        if (FaceFile == null || FaceFile.Length == 0)
            return BadRequest("Face image is required.");

        // 1️⃣ Insert Person
        var person = new Person
        {
            Firstname = FirstName,
            Lastname = LastName,
            Email = Email,
            Password = "123", 
            Status = 10
        };

        _context.Persons.Add(person);
        await _context.SaveChangesAsync(); 

        // 2️⃣ Save image
        var uploadsPath = Path.Combine(_env.WebRootPath, "faces");
        Directory.CreateDirectory(uploadsPath);

        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(FaceFile.FileName)}";
        var filePath = Path.Combine(uploadsPath, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await FaceFile.CopyToAsync(stream);
        }

        // 3️⃣ Insert Picture_Directory
        var picture = new PictureDirectory
        {
            PersonId = person.Id,
            Url = "/faces/" + fileName,
            CreatedAt = DateTime.Now
        };
        _context.PictureDirectory.Add(picture);
        await _context.SaveChangesAsync();

        return new OkResult();
    }
    }


