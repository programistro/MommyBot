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
        if (update.CallbackQuery != null && update.CallbackQuery.Data != null)
        {
            var userId = update.CallbackQuery.Data.Split(',')[0];
            var uesrName = update.CallbackQuery.Data.Split(',')[1].Trim();
            
            if (userId.StartsWith("-no-"))
            {
                await botClient.SendMessage(long.Parse(userId.Remove(0, 3)), "Отказан доступ");

                await botClient.SendMessage(-1002414377806, "Отказано \u274c");
                    
                await bot.DeleteMessage(chatId: update.CallbackQuery.Message.Chat.Id, messageId: update.CallbackQuery.Message.Id,
                    cancellationToken: cancellationToken);
                
                return;
            }
                
            await using var client = new WTelegram.Client(Config);
            var user = await client.LoginUserIfNeeded();
            Console.WriteLine($"We are logged-in as {user.username ?? user.first_name + " " + user.last_name} (id {user.id})");
            
            var chats = await client.Messages_GetAllChats();
            var chat = chats.chats[2297307731]; 
                
            var userUs = await client.Contacts_ResolveUsername(uesrName);
            
            var userChat = new InputUser(long.Parse(userId), userUs.User.access_hash); 
            
            try
            {
                await client.AddChatUser(chat, userChat);

                await botClient.SendMessage(long.Parse(userId), "Вы были одобрены модерацией",
                    cancellationToken: cancellationToken);

                await botClient.SendMessage(-1002414377806, "Принят \u2705", cancellationToken: cancellationToken);

                await bot.DeleteMessage(chatId: update.CallbackQuery.Message.Chat.Id, messageId: update.CallbackQuery.Message.Id,
                    cancellationToken: cancellationToken);
            }
            catch (Exception e)
            {    
                Console.WriteLine(e);
                throw;
            }
                
            return;
        }

        if (!string.IsNullOrEmpty(update?.Message?.Text) && update.Message.Chat.Type != ChatType.Supergroup)
        {
            var chatId = update.Message.Chat.Id;
            var messageText = update.Message.Text;
            
            if (messageText == "/start")
            {
                await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "Привет , я Mommy bot , для вступления в чат и нашего знакомства прошу ответить на несколько вопросов. Начнем с имени:",
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

            await botClient.SendMessage(message.Chat.Id,
                "Отлично! Скоро вашу заявку одобрят!\nПока что можете ознакомиться с правилами чата.\n\n1. Перед началом общения рекомендуется ознакомиться с правилами, представленными на этой странице.\n2. При нарушении правил чата администрация имеет право выдать участнику предупреждение или исключить из чата без возможности разблокировки.\n3. Мы уважаем всех участников чата и просим вас также относиться друг к другу.\n4. Правила чата не оспариваются и не обсуждаются.\n\nРазрешается:\n1. Общаться по теме чата.\n2. Помогать участникам чата в ответах на вопросы, рекомендовать проверенную услугу/товар/специалиста.\n3. Приглашать своих подруг в чат ( ссылка для приглашения https://t.me/main_mommy_bot )\n\nЗапрещается:\n1. Присылать ссылки на сторонние чаты.\n2. Обсуждать темы религии, политики, советовать медицинские препараты.\n3. Материться, оскорблять и дискриминировать других участников чата.\n4. Присылать спам и любые файлы, нарушать авторские права третьих лиц.\n5. Рекламировать свои услуги и продавать товары без согласия администраторов чата.");
    
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
    
            await _modService.SendToModeratorAsync(surveyModel.UserId, 1784802785, message.Chat.Username);
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