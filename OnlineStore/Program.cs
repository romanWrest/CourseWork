using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;
using OnlineStore.Models;
using OnlineStore.Services;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);


// �������� ��� ��������
if (!File.Exists("appsettings.json"))
{
    throw new FileNotFoundException("appsettings.json not found");
}

// ��������� Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();
builder.Host.UseSerilog();

// ���������� ��������
builder.Services.AddControllers();
builder.Services.AddDbContext<OnlineStoreContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<FileService>();

// ����������� ������� ����� ��� ��������� RabbitMQ � Kafka
builder.Services.AddHostedService<NotificationService>(); // ��� ����������� (������� 2 � 4)
builder.Services.AddHostedService<AnalyticsService>();   // ��� ��������� (������� 3)
builder.Services.AddHostedService<MonitoringService>();  // ��� ����������� (������� 5)

// ��������� ��������� ������� �����
builder.Services.Configure<HostOptions>(options =>
{
    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
    Log.Information("Configured HostOptions to ignore background service exceptions.");
});

// ��������� �������������� JWT
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

// ���������� � ������ ����������
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
await app.StartAsync(); // ����������� ������
Log.Information("Application started, running...");

await app.WaitForShutdownAsync(); // �������� ����������
Log.Information("Application stopped.");

