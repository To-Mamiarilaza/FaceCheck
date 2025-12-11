namespace FaceCheck.Models;

using System.ComponentModel.DataAnnotations.Schema;

[Table("Picture_Directory")] 
public class PictureDirectory
{
    public int Id { get; set; }
    public int PersonId { get; set; }
    public string Url { get; set; }
    public DateTime CreatedAt { get; set; }

    public Person Person { get; set; }
}