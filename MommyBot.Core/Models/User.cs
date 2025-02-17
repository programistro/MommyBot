using System.ComponentModel.DataAnnotations;

namespace MommyBot.Core.Models;

public class User
{
    [Key]
    public Guid Id { get; set; }
    
    public string? UserName { get; set; }
    
    public string? Name { get; set; }
    
    public string? LastName { get; set; }
    
    public int Age { get; set; }
    
    public string City { get; set; }
}