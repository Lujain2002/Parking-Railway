using MQTTnet;
using MQTTnet.Client;
using System.Text;
using System.Text.Json;

namespace ParkingSystem.Relay
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly HttpClient _httpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        private IMqttClient _mqttClient;
        private MqttClientOptions _mqttOptions;

        //http://parkly2026-001-site1.rtempurl.com/api/MqttGateway

        //https://localhost:7111/api/MqttGateway
        //http://parkly.runasp.net/MqttGateway
        private const string BaseApiUrl = "http://parkly.runasp.net/api/MqttGateway";

        public Worker(ILogger<Worker> logger) => _logger = logger;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var factory = new MqttFactory();
            _mqttClient = factory.CreateMqttClient();

            
            _mqttOptions = new MqttClientOptionsBuilder()
                .WithTcpServer("c335c915f7a540bcb9d83b6f4b0444f3.s1.eu.hivemq.cloud", 8883)
                .WithCredentials("esp32_worker", "ParkMaster2026")
                .WithTls(new MqttClientOptionsBuilderTlsParameters { UseTls = true })
                .WithCleanSession()
                .WithKeepAlivePeriod(TimeSpan.FromSeconds(30))
                .Build();

        
            _mqttClient.DisconnectedAsync += async e =>
            {
                _logger.LogWarning("Disconnected from HiveMQ. Reason: {0}", e.Reason);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); 

                try
                {
                    if (!stoppingToken.IsCancellationRequested)
                    {
                        await _mqttClient.ConnectAsync(_mqttOptions, stoppingToken);
                        _logger.LogInformation("Reconnected successfully.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Reconnect failed: {ex.Message}");
                }
            };

         
            _mqttClient.ConnectedAsync += async e =>
            {
                await _mqttClient.SubscribeAsync("#");
                _logger.LogInformation("Subscribed to topics after connection.");
            };

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

          
            await ConnectWithRetryAsync(_mqttOptions, stoppingToken);

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
                                try
                                {
                                    var msg = new MqttApplicationMessageBuilder()
                                        .WithTopic(cmd.Topic)
                                        .WithPayload(cmd.Payload)
                                        .Build();

                                    await _mqttClient.PublishAsync(msg, stoppingToken);
                                    _logger.LogInformation($"[API -> MQTT] Sent: {cmd.Payload}");

                                    await _httpClient.PostAsync($"{BaseApiUrl}/confirm-command/{cmd.Id}", null);
                                }
                                catch (Exception ex) { _logger.LogError($"Command Error: {ex.Message}"); }
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
                    _logger.LogInformation("Connected to HiveMQ successfully!");
                }
                catch
                {
                    _logger.LogError("Initial connection failed, retrying in 5s...");
                    await Task.Delay(5000, ct);
                }
            }
        }
    }
}

public class MqttCommand { 
        public int Id { get; set; }
        public string Topic { get; set; }
        public string Payload { get; set; }
    }
