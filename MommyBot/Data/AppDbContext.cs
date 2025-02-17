using Microsoft.EntityFrameworkCore;
using Telegram.Bot.Types;

namespace MommyBot.Data;

public class AppDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
 
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("Data Source=database.db");
    }
}