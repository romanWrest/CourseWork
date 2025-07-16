using Microsoft.Extensions.Hosting;
using Confluent.Kafka;
using Newtonsoft.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OnlineStore.Services
{
    public class AnalyticsService : BackgroundService
    {
        private readonly ConsumerConfig _config;

        public AnalyticsService()
        {
            _config = new ConsumerConfig
            {
                BootstrapServers = "localhost:9092",
                GroupId = "analytics-group",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = true,
                SocketTimeoutMs = 10000
            };
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("AnalyticsService: Starting...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var consumer = new ConsumerBuilder<Ignore, string>(_config).Build();
                    Console.WriteLine("AnalyticsService: Before subscribe...");
                    consumer.Subscribe("user_events");
                    Console.WriteLine("AnalyticsService: Subscribed to user_events");

                    while (!stoppingToken.IsCancellationRequested)
                    {
                        try
                        {
                            var consumeResult = consumer.Consume(1000);
                            if (consumeResult != null)
                            {
                                Console.WriteLine($"AnalyticsService: Received user event - {consumeResult.Message.Value}");
                            }
                            else
                            {
                                Console.WriteLine("AnalyticsService: No messages available, polling...");
                            }
                            await Task.Delay(100, stoppingToken);
                        }
                        catch (ConsumeException ex)
                        {
                            Console.WriteLine($"AnalyticsService: Error consuming - {ex.Message}");
                            await Task.Delay(5000, stoppingToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"AnalyticsService: Failed to initialize - {ex.Message}");
                    await Task.Delay(5000, stoppingToken);
                }
            }
        }
    }
}   