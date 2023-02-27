using EtoZhePackageOpenAI.OpenAIHandler;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

using CancellationTokenSource cts = new();

var configuration = new ConfigurationBuilder()
     .AddJsonFile($"config.json");

var config = configuration.Build();

var botToken = config.GetRequiredSection("Settings").GetValue<string>("Token");

// Get this token via BotFather on telegram
var botClient = new TelegramBotClient(botToken);

var openAiHandler = new OpenAIHandler(config);

Console.WriteLine("Bot have been started " + botClient.GetMeAsync().Result.FirstName);

// StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
ReceiverOptions receiverOptions = new()
{
    AllowedUpdates = Array.Empty<UpdateType>() // receive all update types
};

botClient.StartReceiving(
    updateHandler: HandleUpdateAsync,
    pollingErrorHandler: HandlePollingErrorAsync,
    receiverOptions: receiverOptions,
    cancellationToken: cts.Token
);

var me = await botClient.GetMeAsync();

Console.WriteLine($"Start listening for @{me.Username}");
Console.ReadLine();

// Send cancellation request to stop bot
cts.Cancel();

async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
{
    Console.WriteLine(JsonConvert.SerializeObject(update));

    if (update.Type is not UpdateType.Message)
        return;

    if (update.Message is not { } message)
        return;

    if (message.Text is not { } messageText)
        return;

    var chatId = message.Chat.Id;

    Console.WriteLine($"Received a '{messageText}' message in chat {chatId}.");

    if (message.Text.ToLower() == "/start")
    {
        await botClient.SendTextMessageAsync(message.Chat, "Welcome aboard, traveler!", cancellationToken: cancellationToken);
        return;
    }

    var guess = await openAiHandler.HandleOpenAIRequest(messageText, cancellationToken);

    await botClient.SendTextMessageAsync(message.Chat, guess, cancellationToken: cancellationToken);
}

Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
{
    var ErrorMessage = exception switch
    {
        ApiRequestException apiRequestException
            => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
        _ => exception.ToString()
    };

    Console.WriteLine(ErrorMessage);
    return Task.CompletedTask;
}