using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Serilog;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace OnlineStore.Services
{
    public class NotificationService : BackgroundService
    {
        private readonly IConnectionFactory _connectionFactory;
        private readonly IConfiguration _configuration;
        private int _retryDelayMs = 5000;

        public NotificationService(IConfiguration configuration)
        {
            _connectionFactory = new ConnectionFactory
            {
                HostName = "localhost",
                Port = 5672
            };
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Log.Information("NotificationService: Starting...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var connection = _connectionFactory.CreateConnection();
                    using var channel = connection.CreateModel();

                    Log.Information("NotificationService: Connected to RabbitMQ");

                    channel.QueueDeclare(queue: "order_created_queue",
                        durable: false,
                        exclusive: false,
                        autoDelete: false,
                        arguments: null);

                    channel.QueueDeclare(queue: "order_status_updates",
                        durable: false,
                        exclusive: false,
                        autoDelete: false,
                        arguments: null);

                    var consumer = new EventingBasicConsumer(channel);
                    consumer.Received += async (model, ea) =>
                    {
                        var body = ea.Body.ToArray();
                        var message = Encoding.UTF8.GetString(body);
                        var routingKey = ea.RoutingKey;

                        try
                        {
                            if (routingKey == "order_created_queue")
                            {
                                await ProcessOrderCreated(message);
                            }
                            else if (routingKey == "order_status_updates")
                            {
                                await ProcessStatusUpdate(message);
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"Error processing message: {ex.Message}");
                        }
                    };

                    channel.BasicConsume(queue: "order_created_queue",
                        autoAck: true,
                        consumer: consumer);

                    channel.BasicConsume(queue: "order_status_updates",
                        autoAck: true,
                        consumer: consumer);

                    _retryDelayMs = 5000;
                    while (!stoppingToken.IsCancellationRequested && connection.IsOpen)
                    {
                        await Task.Delay(1000, stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"RabbitMQ connection error: {ex.Message}");
                    await Task.Delay(_retryDelayMs, stoppingToken);
                    _retryDelayMs = Math.Min(_retryDelayMs * 2, 30000);
                }
            }

            Log.Information("NotificationService: Stopped.");
        }

        private async Task ProcessOrderCreated(string message)
        {
            var order = JsonConvert.DeserializeObject<dynamic>(message);
            if (order?.Id == null)
            {
                Log.Warning("Invalid order message");
                return;
            }

            var recipientEmail = order.UserEmail?.ToString() ?? "life.video@list.ru";
            var subject = $"New Order Created - Order ID: {order.Id}";
            var bodyText = $"Dear Customer,\n\nYour order (ID: {order.Id}) has been created successfully.\n" +
                          $"User ID: {order.UserId}\nTotal: {order.TotalPrice}\nDate: {order.OrderDate}\n\n" +
                          $"Thank you for shopping with us!\nOnline Store Team";

            await SendEmailAsync(subject, bodyText, recipientEmail);
            Log.Information($"Email notification sent for order {order.Id}");
        }

        private async Task ProcessStatusUpdate(string message)
        {
            var statusUpdate = JsonConvert.DeserializeObject<dynamic>(message);
            if (statusUpdate?.Id == null)
            {
                Log.Warning("Invalid status update message");
                return;
            }

            var recipientEmail = statusUpdate.UserEmail?.ToString() ?? "danykosov585@gmail.com";
            var subject = $"Order Status Updated - Order ID: {statusUpdate.Id}";
            var bodyText = $"Dear Customer,\n\nThe status of your order (ID: {statusUpdate.Id}) " +
                          $"has been updated to '{statusUpdate.Status}'.\n" +
                          $"User ID: {statusUpdate.UserId}\n\n" +
                          $"Thank you for shopping with us!\nOnline Store Team";

            await SendEmailAsync(subject, bodyText, recipientEmail);
            Log.Information($"Status update sent for order {statusUpdate.Id}");
        }

        private async Task SendEmailAsync(string subject, string body, string recipientEmail)
        {
            var smtpSettings = _configuration.GetSection("SmtpSettings");

            try
            {
                using var client = new SmtpClient();

                // Подключаемся к SMTP-серверу один раз
                await client.ConnectAsync(
                    smtpSettings["Host"] ?? "smtp.yandex.ru",
                    smtpSettings.GetValue<int>("Port", 465),
                    SecureSocketOptions.SslOnConnect);

                await client.AuthenticateAsync(
                    smtpSettings["Username"] ?? throw new ArgumentNullException("Username"),
                    smtpSettings["Password"] ?? throw new ArgumentNullException("Password"));

                for (int i = 1; i <= 1000; i++)
                {
                    try
                    {
                        var message = new MimeMessage();
                        message.From.Add(new MailboxAddress(
                            smtpSettings["SenderName"] ?? "Online Store",
                            smtpSettings["SenderEmail"] ?? throw new ArgumentNullException("SenderEmail")));

                        message.To.Add(MailboxAddress.Parse(recipientEmail));
                        message.Subject = $"{subject} (Attempt {i})"; // Добавляем номер попытки в тему
                        message.Body = new TextPart("plain") { Text = body };

                        await client.SendAsync(message);
                        Log.Information($"Email {i} to {recipientEmail} sent successfully");
                    }
                    catch (SmtpCommandException ex)
                    {
                        Log.Error($"SMTP error for email {i} to {recipientEmail} ({ex.StatusCode}): {ex.Message}");
                        // Продолжаем цикл, чтобы не прерывать остальные отправки
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Email {i} sending error to {recipientEmail}: {ex.Message}");
                        // Продолжаем цикл
                    }
                }

                // Отключаемся после всех отправок
                await client.DisconnectAsync(true);
            }
            catch (AuthenticationException ex)
            {
                Log.Error($"SMTP authentication failed: {ex.Message}");
                throw;
            }
            catch (SmtpCommandException ex)
            {
                Log.Error($"SMTP connection error: {ex.StatusCode}: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Log.Error($"SMTP client initialization error: {ex.Message}");
                throw;
            }
        }
    }
}