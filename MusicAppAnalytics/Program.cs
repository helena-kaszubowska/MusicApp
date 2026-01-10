using Amazon.SQS;
using Amazon.SQS.Model;
using MusicAppAnalytics.Services;
using MusicAppAPI.Models;
using Newtonsoft.Json;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add AWS Lambda Hosting
builder.Services.AddAWSLambdaHosting(LambdaEventSource.RestApi);

// AWS Logging
builder.Logging.AddAWSProvider(builder.Configuration.GetAWSLoggingConfigSection());
builder.Logging.SetMinimumLevel(LogLevel.Debug);

// Load configuration from AWS Parameter Store
if (!builder.Environment.IsDevelopment())
{
    builder.Configuration.AddSystemsManager("/music-app/analytics", TimeSpan.FromMinutes(5));
}

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS configuration
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(
        policy =>
        {
            policy.AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});

builder.Services.AddSingleton<AlbumAnalyticsService>();
builder.Services.AddSingleton<TrackAnalyticsService>();
builder.Services.AddLogging(config =>
{
    config.ClearProviders();
    config.AddConfiguration(builder.Configuration.GetSection("Logging"));
    config.AddDebug();
    config.AddEventSourceLogger();
    if (builder.Environment.IsDevelopment())
        config.AddConsole();
});

// AWS SQS Configuration
var sqsConfig = new AmazonSQSConfig();
var serviceUrl = builder.Configuration["SQS:ServiceURL"];
if (!string.IsNullOrEmpty(serviceUrl))
{
    sqsConfig.ServiceURL = serviceUrl;
}
var sqsClient = new AmazonSQSClient(sqsConfig);
builder.Services.AddSingleton<IAmazonSQS>(sqsClient);

// Background service to poll SQS
builder.Services.AddHostedService<SqsBackgroundService>();

WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
