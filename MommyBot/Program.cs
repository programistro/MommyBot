using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MommyBot.Data;
using MommyBot.Service;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using WTelegram;
using User = WTelegram.Types.User;
using TL;

namespace MommyBot;

class Program
{   
    public class SurveyState
    {
        public string Name { get; set; }
        public int Age { get; set; }
        public string City { get; set; }
        public SurveyStep CurrentStep { get; set; }

        public enum SurveyStep
        {
            None,
            WaitingForName,
            WaitingForAge,
            WaitingForCity,
            Completed
        }
    }

    private static ITelegramBotClient bot;
    private static DatabaseService _databaseService;
    private static ModerationService _modService;
    private static Dictionary<long, SurveyState> _surveyStates = new();
    private static string _groupId = "-2266535283"; // ID вашей группы
    
    static async Task Main(string[] args)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
        
        var options = new DbContextOptionsBuilder<SurveyContext>()
            .UseNpgsql("host=89.110.95.169;port=5432;Username=admin;Password=Abc#1234;Database=postgres")
            .Options;
        
        // builder.Services.AddDbContextFactory<SurveyContext>(options => options.UseSqlite("Data Source=surveys.db"));
    
        bot = new TelegramBotClient("7625037901:AAHR5iEPO8_lNOqOAIiVKAZgjMJxyQA361s");
    
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = { } 
        };
        
        _databaseService = new DatabaseService(options);
        _modService = new ModerationService(_databaseService, bot);
    
        bot.StartReceiving(
            updateHandler: HandleUpdateAsync,
            errorHandler: HandlePollingErrorAsync,
            receiverOptions: receiverOptions);
    
        var me = await bot.GetMeAsync();
        Console.WriteLine($"Бот {me.Username} запущен!");

        await Task.Delay(-1);
        // Console.ReadLine();
        // await bot.StopReceiving();
    }
    
    private static async Task HandleUpdateAsync(ITelegramBotClient botClient, Telegram.Bot.Types.Update update, CancellationToken cancellationToken)
    {
        
        if (update.CallbackQuery != null)
        {
            if (update.CallbackQuery.Data != null)
            {
                await using var client = new WTelegram.Client(Config);
                var user = await client.LoginUserIfNeeded();
                Console.WriteLine($"We are logged-in as {user.username ?? user.first_name + " " + user.last_name} (id {user.id})");
            
                var chats = await client.Messages_GetAllChats();
                var chat = chats.chats[2420062922]; 
            
                var userChat = new InputUser(long.Parse(update.CallbackQuery.Data), 0); 
            
                await client.AddChatUser(chat, userChat);
                
                return;       
            }
        }

        if (!string.IsNullOrEmpty(update.Message.Text))
        {
            var chatId = update.Message.Chat.Id;
            var messageText = update.Message.Text;
            
            if (messageText == "/start")
            {
                await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "Здравствуйте! Давайте заполним анкету.\nПожалуйста, введите ваше имя:",
                    cancellationToken: cancellationToken);
    
                if (_surveyStates.ContainsKey(chatId))
                {
                    _surveyStates[chatId] = new SurveyState { CurrentStep = SurveyState.SurveyStep.WaitingForName };
                }
                else
                {
                    _surveyStates.Add(chatId, new SurveyState { CurrentStep = SurveyState.SurveyStep.WaitingForName });
                }
    
                return;
            }
    
            // Если нет активного опроса, начинаем новый
            if (!_surveyStates.ContainsKey(chatId))
            {
                _surveyStates[chatId] = new SurveyState { CurrentStep = SurveyState.SurveyStep.WaitingForName };
        
                await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "Давайте заполним анкету! Пожалуйста, введите ваше имя:",
                    cancellationToken: cancellationToken);
        
                return;
            }
    
            var currentState = _surveyStates[chatId];
    
            switch (currentState.CurrentStep)
            {
                case SurveyState.SurveyStep.WaitingForName:
                    await HandleNameInput(botClient, update.Message, currentState);
                    break;
            
                case SurveyState.SurveyStep.WaitingForAge:
                    await HandleAgeInput(botClient, update.Message, currentState);
                    break;
            
                case SurveyState.SurveyStep.WaitingForCity:
                    await HandleCityInput(botClient, update.Message, currentState);
                    break;
            }
        }
    }
    
    private static async Task HandleNameInput(ITelegramBotClient botClient, Telegram.Bot.Types.Message message, SurveyState state)
    {
        if (!string.IsNullOrWhiteSpace(message.Text))
        {
            state.Name = message.Text;
            state.CurrentStep = SurveyState.SurveyStep.WaitingForAge;
            
            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "Пожалуйста, введите ваш возраст:",
                cancellationToken: default);
        }
    }
    
    private static async Task HandleAgeInput(ITelegramBotClient botClient, Telegram.Bot.Types.Message message, SurveyState state)
    {
        if (int.TryParse(message.Text, out int age))
        {
            if (age > 0 && age < 150)
            {
                state.Age = age;
                state.CurrentStep = SurveyState.SurveyStep.WaitingForCity;
                
                await botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: "Отлично! Теперь введите ваш город:",
                    cancellationToken: default);
            }
            else
            {
                await botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: "Пожалуйста, введите корректный возраст от 1 до 150 лет.",
                    cancellationToken: default);
            }
        }
        else
        {
            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "Возраст должен быть числом!",
                cancellationToken: default);
        }
    }
    
    private static async Task HandleCityInput(ITelegramBotClient botClient, Telegram.Bot.Types.Message message, SurveyState state)
    {
        if (!string.IsNullOrWhiteSpace(message.Text))
        {
            state.City = message.Text;
            state.CurrentStep = SurveyState.SurveyStep.Completed;
    
            var surveyResult = $"Результат анкетирования:\n" +
                             $"Имя: {state.Name}\n" +
                             $"Возраст: {state.Age}\n" +
                             $"Город: {state.City}";
    
            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: surveyResult,
                cancellationToken: default);
    
            _surveyStates.Remove(message.Chat.Id);
            
            var surveyModel = new Survey
            {
                Id = Guid.NewGuid(),
                Name = state.Name,
                Age = state.Age,
                City = state.City,
                UserId = message.Chat.Id
            };
    
            await _databaseService.SaveSurveyAsync(surveyModel);
    
            await _modService.SendToModeratorAsync(surveyModel.UserId, 1784802785);
        }
    }
    
    private static Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        var ErrorMessage = exception switch
        {
            ApiRequestException apiEx => $"Ошибка API:\n[{apiEx.ErrorCode}]\n{apiEx.Message}",
            _ => exception.ToString()
        };
        
        Console.WriteLine(ErrorMessage);
        return Task.CompletedTask;
    }
    
    static string Config(string what)
    {
        switch (what)
        {
            case "api_id": return "25421922";
            case "api_hash": return "8ed9b2cb68b4b22166105e81ddafb969";
            case "phone_number": return "+79204489841";
            case "verification_code": Console.Write("Code: "); return Console.ReadLine();
            default: return null;                  // let WTelegramClient decide the default config
        }
    }
}