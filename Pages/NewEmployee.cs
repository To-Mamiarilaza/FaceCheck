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
    public string Password { get; set; }

    [BindProperty]
    public string ConfirmPassword { get; set; }

    [BindProperty]
    public List<IFormFile> FaceFiles { get; set; }

    public void OnGet()
    {
        // initialisation si nécessaire
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (FaceFiles == null || FaceFiles.Count == 0 || FaceFiles.All(f => f.Length == 0))
            return BadRequest("At least one face image is required.");
        
        if (string.IsNullOrEmpty(Password))
            return BadRequest("Password is required.");

        if (Password != ConfirmPassword)
            return BadRequest("Passwords do not match.");

        // insert Person
        var person = new Person
        {
            Firstname = FirstName,
            Lastname = LastName,
            Email = Email,
            Password = Password, 
            Status = 1
        };

        _context.Persons.Add(person);
        await _context.SaveChangesAsync(); 

        // save image
        var uploadsPath = Path.Combine(_env.WebRootPath, "faces");
        Directory.CreateDirectory(uploadsPath);

        foreach (var faceFile in FaceFiles)
        {
            if (faceFile.Length > 0)
            {
                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(faceFile.FileName)}";
                var filePath = Path.Combine(uploadsPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await faceFile.CopyToAsync(stream);
                }

                // 3️⃣ Insert Picture_Directory
                var picture = new PictureDirectory
                {
                    PersonId = person.Id,
                    Url = "/faces/" + fileName,
                    CreatedAt = DateTime.Now
                };
                _context.PictureDirectory.Add(picture);
            }
        }
        await _context.SaveChangesAsync();

        return new OkResult();
    }
    }


