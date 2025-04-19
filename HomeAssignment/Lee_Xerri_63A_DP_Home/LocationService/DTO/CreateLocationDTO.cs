using System.ComponentModel.DataAnnotations;

namespace LocationService.DTO
{
    public class CreateLocationDTO
    {
        [Required] public string Name { get; set; }
    }
}
