using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace MommyBot.Data;

public class SurveyContext : DbContext
{
    // public SurveyContext(DbContextOptions<SurveyContext> options)
    //     : base(options)
    // {
    // }

    public DbSet<Survey> Surveys => Set<Survey>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Moderator> Moderators => Set<Moderator>();
    public DbSet<Moderation> Moderations => Set<Moderation>();
    public DbSet<Group> Groups => Set<Group>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseNpgsql("host=89.110.95.169;port=5432;Username=admin;Password=Abc#1234;Database=postgres");
    }
}

public class Survey
{
    [Key]
    public Guid Id { get; set; }
    public string Name { get; set; }
    public int Age { get; set; }
    public string City { get; set; }
    public long UserId { get; set; }
}

public class User
{
    [Key]
    public Guid Id { get; set; }
    public long TelegramId { get; set; }
    public string Username { get; set; }
    public string Status { get; set; }
    public string? GroupId { get; set; }
}

public class Moderator
{
    [Key]
    public Guid Id { get; set; }
    public long TelegramId { get; set; }
    public string Username { get; set; }
}

public class Moderation
{
    [Key]
    public Guid Id { get; set; }
    public long UserId { get; set; }
    public long? ModeratorId { get; set; }
}

public class Group
{
    [Key]
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string ChatId { get; set; }
}