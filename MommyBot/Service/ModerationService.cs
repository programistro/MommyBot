using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace MommyBot.Service;

public class ModerationService
{
    private readonly DatabaseService _databaseService;
    private readonly ITelegramBotClient _bot;

    public ModerationService(DatabaseService databaseService, ITelegramBotClient bot)
    {
        _databaseService = databaseService;
        _bot = bot;
    }

    public async Task SendToModeratorAsync(long userId, long moderatorId, string username)
    {
        var user = await _databaseService.GetUserAsync(userId);
        var survey = await _databaseService.GetSurveyAsync(userId);

        if (user == null || survey == null)
            return;

        var keyboard = new InlineKeyboardMarkup(new[]
        {
            InlineKeyboardButton.WithCallbackData("Одобрить", $"{userId}, {username}"),
            InlineKeyboardButton.WithCallbackData("Отказать", $"{userId}, {username}"),
        });

        await _bot.SendTextMessageAsync(
            chatId: -1002414377806,
            text: $"Новая анкета для модерации:\n" +
                  $"Имя: {survey.Name}\n" +
                  $"Возраст: {survey.Age}\n" +
                  $"Город: {survey.City}\n"+
                  $"Username: {username}",  
            replyMarkup: keyboard);
    }

    public async Task HandleModerationCallback(long userId, long moderatorId)
    {
        try
        {
            await _databaseService.ApproveUserAsync(userId, moderatorId);
            
            await _bot.SendTextMessageAsync(
                chatId: userId,
                text: "Ваша анкета одобрена! Вы были добавлены в группу.");

            await _bot.SendTextMessageAsync(
                chatId: moderatorId,
                text: "Пользователь успешно добавлен в группу.");
        }
        catch (Exception ex)
        {
            await _bot.AnswerCallbackQueryAsync(
                callbackQueryId: $"approve_{userId}",
                text: "Произошла ошибка при добавлении в группу.");
            Console.WriteLine($"Ошибка при модерации: {ex.Message}");
        }
    }
}