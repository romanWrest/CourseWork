using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;
using OnlineStore.Models;
using OnlineStore.Services;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);


// Добавьте эту проверку
if (!File.Exists("appsettings.json"))
{
    throw new FileNotFoundException("appsettings.json not found");
}

// Настройка Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();
builder.Host.UseSerilog();

// Добавление сервисов
builder.Services.AddControllers();
builder.Services.AddDbContext<OnlineStoreContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<FileService>();

// Регистрация фоновых служб для обработки RabbitMQ и Kafka
builder.Services.AddHostedService<NotificationService>(); // Для уведомлений (Задания 2 и 4)
builder.Services.AddHostedService<AnalyticsService>();   // Для аналитики (Задание 3)
builder.Services.AddHostedService<MonitoringService>();  // Для мониторинга (Задание 5)

// Настройка поведения фоновых служб
builder.Services.Configure<HostOptions>(options =>
{
    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
    Log.Information("Configured HostOptions to ignore background service exceptions.");
});

// Настройка аутентификации JWT
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Построение и запуск приложения
Log.Information("Building application...");
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
//app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

Log.Information("Starting application...");
await app.StartAsync(); // Асинхронный запуск
Log.Information("Application started, running...");

await app.WaitForShutdownAsync(); // Ожидание завершения
Log.Information("Application stopped.");

