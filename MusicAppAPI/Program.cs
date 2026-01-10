using System.Text;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.SimpleNotificationService;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MusicAppAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// AWS Logging
builder.Logging.AddAWSProvider(builder.Configuration.GetAWSLoggingConfigSection());
builder.Logging.SetMinimumLevel(LogLevel.Debug);

// Load configuration from AWS Parameter Store
// In production, you would typically use the default credentials chain.
// For local development with LocalStack, you might need custom configuration or just rely on appsettings.
if (!builder.Environment.IsDevelopment())
{
    builder.Configuration.AddSystemsManager("/music-app/api", TimeSpan.FromMinutes(5));
}

// DynamoDB connection
var dynamoDbConfig = new AmazonDynamoDBConfig();
var serviceUrl = builder.Configuration["DynamoDB:ServiceURL"];
if (!string.IsNullOrEmpty(serviceUrl))
{
    dynamoDbConfig.ServiceURL = serviceUrl;
}

var client = new AmazonDynamoDBClient(dynamoDbConfig);
builder.Services.AddSingleton<IAmazonDynamoDB>(client);
builder.Services.AddSingleton<IDynamoDBContext, DynamoDBContext>();

// AWS SNS Configuration
var snsConfig = new AmazonSimpleNotificationServiceConfig();
var snsServiceUrl = builder.Configuration["SNS:ServiceURL"];
if (!string.IsNullOrEmpty(snsServiceUrl))
{
    snsConfig.ServiceURL = snsServiceUrl;
}
var snsClient = new AmazonSimpleNotificationServiceClient(snsConfig);
builder.Services.AddSingleton<IAmazonSimpleNotificationService>(snsClient);

// JWT Configuration
var jwtKey = builder.Configuration["JWT_KEY"];
if (string.IsNullOrEmpty(jwtKey))
{
    throw new InvalidOperationException("JWT_KEY is not configured.");
}
var key = Encoding.ASCII.GetBytes(jwtKey);

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = false,
            ValidateAudience = false
        };
    });

builder.Services.AddScoped<IUserService, UserService>();

builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "MusicAppAPI", Version = "v1" });

    // Authorization configuration Swagger UI
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection();

app.UseCors();

// Enabling JWT Auth
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
