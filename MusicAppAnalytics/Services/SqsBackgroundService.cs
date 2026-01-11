using Amazon.SQS;
using Amazon.SQS.Model;
using MusicAppAPI.Models;
using Newtonsoft.Json;

namespace MusicAppAnalytics.Services;

public class SqsBackgroundService : BackgroundService
{
    private readonly IAmazonSQS _sqsClient;
    private readonly AlbumAnalyticsService _albumAnalyticsService;
    private readonly TrackAnalyticsService _trackAnalyticsService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SqsBackgroundService> _logger;

    public SqsBackgroundService(IAmazonSQS sqsClient, AlbumAnalyticsService albumAnalyticsService, TrackAnalyticsService trackAnalyticsService, IConfiguration configuration, ILogger<SqsBackgroundService> logger)
    {
        _sqsClient = sqsClient;
        _albumAnalyticsService = albumAnalyticsService;
        _trackAnalyticsService = trackAnalyticsService;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var queueUrl = _configuration["SQS:QueueUrl"];
        _logger.LogInformation("Starting SQS polling on queue: {QueueUrl}", queueUrl);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var request = new ReceiveMessageRequest
                {
                    QueueUrl = queueUrl,
                    MaxNumberOfMessages = 10,
                    WaitTimeSeconds = 20,
                    MessageAttributeNames = new List<string> { "All" }
                };

                var response = await _sqsClient.ReceiveMessageAsync(request, stoppingToken);

                if (response.Messages.Count > 0)
                {
                    _logger.LogDebug("Received {Count} messages from SQS", response.Messages.Count);
                }

                foreach (var message in response.Messages)
                {
                    await ProcessMessageAsync(message);
                    await _sqsClient.DeleteMessageAsync(queueUrl, message.ReceiptHandle, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing SQS message");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    private async Task ProcessMessageAsync(Message message)
    {
        try 
        {
            // SNS messages are wrapped in a JSON object with a "Message" property containing the actual payload
            // and "MessageAttributes" if sent via SNS.
            // However, if we consume directly from SQS (without SNS subscription), the format might differ.
            // Assuming SNS -> SQS subscription:
            
            var snsMessage = JsonConvert.DeserializeObject<dynamic>(message.Body);
            string payload = snsMessage?.Message ?? "";
            string messageType = snsMessage?.MessageAttributes?.MessageType?.Value ?? "";

            if (string.IsNullOrEmpty(payload))
            {
                // Fallback: maybe it's a direct SQS message
                payload = message.Body;
                if (message.MessageAttributes.TryGetValue("MessageType", out var attr))
                {
                    messageType = attr.StringValue;
                }
            }

            if (messageType == "AlbumViewed")
            {
                var albumViewedMessage = JsonConvert.DeserializeObject<AlbumViewedMessage>(payload);
                if (albumViewedMessage != null)
                {
                    _albumAnalyticsService.RecordView(albumViewedMessage);
                    _logger.LogInformation("Processed AlbumViewed message for album: {AlbumId}", albumViewedMessage.AlbumId);
                }
            }
            else if (messageType == "TrackDownloaded")
            {
                var trackDownloadedMessage = JsonConvert.DeserializeObject<TrackDownloadedMessage>(payload);
                if (trackDownloadedMessage != null)
                {
                    _trackAnalyticsService.RecordDownload(trackDownloadedMessage);
                    _logger.LogInformation("Processed TrackDownloaded message for track: {TrackId}", trackDownloadedMessage.TrackId);
                }
            }
            else 
            {
                _logger.LogWarning("Unknown message type: {MessageType}", messageType);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deserializing or processing message body");
        }
    }
}
