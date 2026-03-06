using MQTTnet;
using MQTTnet.Client;
using System.Text;
using System.Text.Json;

namespace ParkingSystem.Relay
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly HttpClient _httpClient = new();
        private IMqttClient _mqttClient;

        //http://parkly2026-001-site1.rtempurl.com/api/MqttGateway
        //https://localhost:7111/api/MqttGateway
        private const string BaseApiUrl = "http://parkly2026-001-site1.rtempurl.com/api/MqttGateway;

        public Worker(ILogger<Worker> logger) => _logger = logger;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var factory = new MqttFactory();
            _mqttClient = factory.CreateMqttClient();

            var options = new MqttClientOptionsBuilder()
                .WithTcpServer("c335c915f7a540bcb9d83b6f4b0444f3.s1.eu.hivemq.cloud", 8883)
                .WithCredentials("esp32_worker", "ParkMaster2026")
                .WithTls(new MqttClientOptionsBuilderTlsParameters { UseTls = true })
                .Build();

           
            _mqttClient.ApplicationMessageReceivedAsync += async e =>
            {
                string topic = e.ApplicationMessage.Topic;
                if (topic.EndsWith("/cmd")) return;

                try
                {
                    var payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                    _logger.LogInformation($"[MQTT -> API] Data: {payload}");

                    
                    await _httpClient.PostAsJsonAsync($"{BaseApiUrl}/update-reading",
                        JsonSerializer.Deserialize<object>(payload), stoppingToken);
                }
                catch (Exception ex) { _logger.LogError($"Relay Post Error: {ex.Message}"); }
            };

        
            await ConnectWithRetryAsync(options, stoppingToken);

            
            while (!stoppingToken.IsCancellationRequested)
            {
                if (_mqttClient.IsConnected)
                {
                    try
                    {
                        var commands = await _httpClient.GetFromJsonAsync<List<MqttCommand>>($"{BaseApiUrl}/pending-commands", stoppingToken);
                        if (commands != null && commands.Any())
                        {
                            foreach (var cmd in commands)
                            {
                                var msg = new MqttApplicationMessageBuilder()
                                    .WithTopic(cmd.Topic)
                                    .WithPayload(cmd.Payload)
                                    .Build();
                                await _mqttClient.PublishAsync(msg, stoppingToken);
                                _logger.LogInformation($"[API -> MQTT] Sent: {cmd.Payload} to {cmd.Topic}");
                            }
                        }
                    }
                    catch (Exception ex) { _logger.LogWarning($"Polling failed: {ex.Message}"); }
                }
                await Task.Delay(2000, stoppingToken);
            }
        }

        private async Task ConnectWithRetryAsync(MqttClientOptions options, CancellationToken ct)
        {
            while (!_mqttClient.IsConnected && !ct.IsCancellationRequested)
            {
                try
                {
                    await _mqttClient.ConnectAsync(options, ct);
                    await _mqttClient.SubscribeAsync("#");
                    _logger.LogInformation("Connected to HiveMQ successfully!");
                }
                catch
                {
                    _logger.LogError("Connection failed, retrying in 5s...");
                    await Task.Delay(5000, ct);
                }
            }
        }
    }

    public class MqttCommand { public string Topic { get; set; } public string Payload { get; set; } }
}