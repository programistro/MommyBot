using Microsoft.EntityFrameworkCore;
using MommyBot.Data;

namespace MommyBot.Service;

public class DatabaseService
{
    private readonly DbContextOptions<SurveyContext> _options;
    private readonly SurveyContext _context;

    public DatabaseService(DbContextOptions<SurveyContext> options)
    {
        _options = options;
        _context = new SurveyContext();
    }

    public async Task SaveSurveyAsync(Survey survey)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.TelegramId == survey.UserId);

        if (user != null)
        {
            var surveyFind = await _context.Surveys.FirstOrDefaultAsync(x => x.UserId == user.TelegramId);
            
            surveyFind.Age = survey.Age;
            surveyFind.Name = survey.Name;
            surveyFind.City = survey.City;
            
            _context.Surveys.Update(surveyFind);
            await _context.SaveChangesAsync();
        }
        if (user == null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                TelegramId = survey.UserId,
                Username = survey.Name,
                Status = "pending",
                GroupId = "wd"
            };
            _context.Users.Add(user);
            
            var surveyEntity = new Survey
            {
                Id = Guid.NewGuid(),
                Name = survey.Name,
                Age = survey.Age,
                City = survey.City,
                UserId = user.TelegramId,
            };

            _context.Surveys.Add(surveyEntity);
            await _context.SaveChangesAsync();
        }
    }

    public async Task SendToModerationAsync(Survey survey)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.TelegramId == survey.UserId);

        var moderation = new Moderation
        {
            Id = Guid.NewGuid(),
            UserId = user.TelegramId,
        };

        _context.Moderations.Add(moderation);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> IsModeratorAsync(long telegramId)
    {
        return await _context.Moderators
            .AnyAsync(m => m.TelegramId == telegramId);
    }

    public async Task AddModeratorAsync(long telegramId, string username)
    {
        var moderator = new Moderator
        {
            Id = Guid.NewGuid(),
            TelegramId = telegramId,
            Username = username
        };

        _context.Moderators.Add(moderator);
        await _context.SaveChangesAsync();
    }

    public async Task ApproveUserAsync(long userId, long moderatorId)
    {
        var moderation = await _context.Moderations
            // .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.UserId == userId);

        if (moderation != null)
        {
            moderation.ModeratorId = moderatorId;

            // moderation.User.Status = "approved";
            var user = await GetUserAsync(moderation.UserId);
            
            user.Status = "approved";
            
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
        }
    }

    public async Task AddUserToGroupAsync(long userId, string groupId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.TelegramId == userId);
        if (user != null)
        {
            var group = await _context.Groups
                .FirstOrDefaultAsync(g => g.ChatId == groupId);
            if (group != null)
            {
                user.GroupId = group.ChatId;
                _context.Users.Update(user);
                await _context.SaveChangesAsync();
            }
        }
    }

    public async Task<User> GetUserAsync(long userId)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.TelegramId == userId);
    }

    public async Task<Survey> GetSurveyAsync(long userId)
    {
        return await _context.Surveys
            .FirstOrDefaultAsync(s => s.UserId == userId);
    }
}