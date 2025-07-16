using Microsoft.Extensions.Hosting;
using Confluent.Kafka;
using System.Threading;
using System.Threading.Tasks;

namespace OnlineStore.Services
{
    public class MonitoringService : BackgroundService
    {
        private readonly ConsumerConfig _config;

        public MonitoringService()
        {
            _config = new ConsumerConfig
            {
                BootstrapServers = "localhost:9092",
                GroupId = "monitoring-group",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = true, // ���������� ��������
                SocketTimeoutMs = 10000 // ����-��� ������
            };
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("MonitoringService: Starting...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var consumer = new ConsumerBuilder<Ignore, string>(_config).Build();
                    Console.WriteLine("MonitoringService: Before subscribe...");
                    consumer.Subscribe("system_metrics");
                    Console.WriteLine("MonitoringService: Subscribed to system_metrics");

                    while (!stoppingToken.IsCancellationRequested)
                    {
                        try
                        {
                            // ���������� Consume � ����-����� ������ ������������ ��������
                            var consumeResult = consumer.Consume(1000); // 1 ������� ��������
                            if (consumeResult != null)
                            {
                                Console.WriteLine($"MonitoringService: Received metric - {consumeResult.Message.Value}");
                            }
                            else
                            {
                                Console.WriteLine("MonitoringService: No messages available, polling...");
                            }
                            await Task.Delay(100, stoppingToken); // ��������� �������� ��� �������� ��������
                        }
                        catch (ConsumeException ex)
                        {
                            Console.WriteLine($"MonitoringService: Error consuming - {ex.Message}");
                            await Task.Delay(5000, stoppingToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"MonitoringService: Failed to initialize - {ex.Message}");
                    await Task.Delay(5000, stoppingToken);
                }
            }

            Console.WriteLine("MonitoringService: Stopped.");
        }
    }
}