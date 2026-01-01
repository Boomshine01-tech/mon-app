namespace SmartNest.Server.Models
{
    public class MqttDataRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
    }

    public class BrokerConnectionRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string BrokerUrl { get; set; } = string.Empty;
        public int Port { get; set; } = 1883;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}