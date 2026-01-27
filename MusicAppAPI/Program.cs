using System.Text;
using EasyNetQ;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MongoDB.Driver;
using MusicAppAPI.Models;
using MusicAppAPI.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Database connection
var mongoClient = new MongoClient($"mongodb+srv://{Environment.GetEnvironmentVariable("DB_USER")}:{Environment.GetEnvironmentVariable("DB_PASSWORD")}@mongocluster.yl7u1.mongodb.net/?retryWrites=true&w=majority&appName=MongoCluster");
var database = mongoClient.GetDatabase(Environment.GetEnvironmentVariable("DB_NAME"));

builder.Services.AddSingleton(database);
builder.Services.AddSingleton(database.GetCollection<User>("users"));
builder.Services.AddSingleton(database.GetCollection<Track>("tracks"));
builder.Services.AddSingleton(database.GetCollection<Album>("albums"));

// Register Password Hasher
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

// JWT Configuration
byte[] key = Encoding.ASCII.GetBytes(Environment.GetEnvironmentVariable("JWT_KEY")!);

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

// Register RabbitMQ
var rabbitMQHost = builder.Configuration.GetValue<string>("RabbitMQ:Host") ?? "rabbitmq";
var connectionString = $"host={rabbitMQHost};port=5672;username=guest;password=guest";
builder.Services.AddSingleton(RabbitHutch.CreateBus(connectionString));

WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Global Exception Handler
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        
        var exceptionHandlerPathFeature = context.Features.Get<IExceptionHandlerPathFeature>();
        var exception = exceptionHandlerPathFeature?.Error;
        
        var errorMessage = exception?.Message ?? "An unexpected error occurred.";
        // In production, you might want to hide the actual exception message
        await context.Response.WriteAsync($"{{\"error\": \"{errorMessage}\"}}");
    });
});

// app.UseHttpsRedirection();

// Middleware to set RabbitMQ host in HttpContext.Items
app.Use(async (context, next) =>
{
    context.Items["RabbitMQHost"] = rabbitMQHost;
    await next(context);
});

app.UseCors();

// Enabling JWT Auth
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();