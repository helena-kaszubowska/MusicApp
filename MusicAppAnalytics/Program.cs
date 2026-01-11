using Amazon.SQS;
using AWS.Logger;
using MusicAppAnalytics.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// AWS Logging (Configured in code, no appsettings dependency)
var awsLoggingConfig = new AWSLoggerConfig
{
    LogGroup = "MusicAppAnalytics",
    Region = "eu-north-1"
};
builder.Logging.AddAWSProvider(awsLoggingConfig);
builder.Logging.SetMinimumLevel(LogLevel.Debug);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(80);
});

// Load configuration from AWS Parameter Store
builder.Configuration.AddSystemsManager("/music-app/analytics", TimeSpan.FromMinutes(5));

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
    // Re-add AWS Provider to ensure it's in the final provider list if cleared
    config.AddAWSProvider(awsLoggingConfig);
    config.AddDebug();
    config.AddEventSourceLogger();
    if (builder.Environment.IsDevelopment())
        config.AddConsole();
});

// AWS SQS Configuration
var sqsConfig = new AmazonSQSConfig();
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
