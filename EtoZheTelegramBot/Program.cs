using System.Text;
using EtoZheTelegramBot.Models;
using Newtonsoft.Json;

using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

using CancellationTokenSource cts = new();

// Get this token via BotFather on telegram
var botClient = new TelegramBotClient("{YOUR_ACCESS_TOKEN_HERE}");
var httpClient = new HttpClient();

httpClient.DefaultRequestHeaders.Add("authorization", "Bearer {YOUR_OPEN_AI_ACCESS_TOKEN_HERE}");

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

    var json = JsonConvert.SerializeObject(
        new OpenAIRequest
        {
            Model = "text-davinci-003", // or 001 cheaper
            Prompt = messageText,
            Temperature = 1, // 0.7 or lower result will be more accuracy
            MaxTokens = 500, // how many symbols in response text
        });

    var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/completions")
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    using var response = await httpClient.SendAsync(request, cancellationToken);
    var responseJson = await response.Content.ReadAsStringAsync();

    if (!response.IsSuccessStatusCode)
    {
        await botClient.SendTextMessageAsync(
            message.Chat,
            $"Try again later openAI has an error RequestContent: {json}, StatusCode: '{(int)response.StatusCode}', ResponseContent: '{responseJson}'",
            cancellationToken: cancellationToken);
        return;
    }

    try
    {
        var dyData = JsonConvert.DeserializeObject<dynamic>(responseJson);

        var guess = dyData!.choices[0].text;

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"---> My guess is: {guess}");
        Console.ResetColor();
        await botClient.SendTextMessageAsync(message.Chat, $"{guess}", cancellationToken: cancellationToken);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"---> Could not deserialize the JSON: {ex.Message}");
    }
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