using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace MommyBot;

class Program
{
    // static void Main(string[] args)
    // {
    //     var client = new TelegramBotClient("7625037901:AAHR5iEPO8_lNOqOAIiVKAZgjMJxyQA361s");
    //     Console.WriteLine("Done");
    //     client.StartReceiving(Start, Error);
    //     Console.ReadLine();
    // }
    //
    // private static async Task Start(ITelegramBotClient botClient, Update update, CancellationToken token)
    // {
    //     var message = update.Message;
    //
    //     if (!string.IsNullOrEmpty(message?.Text))
    //     {
    //         Console.WriteLine($@"{message.Chat.FirstName}  |  {message.Text}");
    //
    //         if (message.Text.StartsWith("/start"))
    //         {
    //             await botClient.SendTextMessageAsync(message.Chat.Id, "Введите свои данные для входа:(фио, возраст и город прожвиания)");
    //         }
    //     }
    // }
    //
    // private static async Task Error(ITelegramBotClient client, Exception exception, CancellationToken token)
    // {
    //     Console.WriteLine(exception.InnerException?.Message);
    //     Console.WriteLine(exception.Message);
    //     Console.WriteLine(exception.Source);
    // }
    
    private static ITelegramBotClient bot;
    private static Dictionary<long, SurveyState> _surveyStates = new();
    
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

    static async Task Main(string[] args)
    {
        bot = new TelegramBotClient("7625037901:AAHR5iEPO8_lNOqOAIiVKAZgjMJxyQA361s");
        
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = { } // получаем все обновления
        };

        bot.StartReceiving(
            updateHandler: HandleUpdateAsync,
            errorHandler: HandlePollingErrorAsync,
            receiverOptions: receiverOptions);

        var me = await bot.GetMeAsync();
        Console.WriteLine($"Бот {me.Username} запущен!");
        
        Console.ReadLine();
        // await bot.StopReceiving();
    }

    private static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Type != UpdateType.Message)
            return;

        if (update.Message!.Type != MessageType.Text)
            return;

        var chatId = update.Message.Chat.Id;
        var messageText = update.Message.Text;

        // Проверяем команду /start
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

    private static async Task HandleNameInput(ITelegramBotClient botClient, Message message, SurveyState state)
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

    private static async Task HandleAgeInput(ITelegramBotClient botClient, Message message, SurveyState state)
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

    private static async Task HandleCityInput(ITelegramBotClient botClient, Message message, SurveyState state)
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
}