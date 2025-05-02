using System.Text.Json;

namespace WebApp.Models
{
    public class NotificationDTO
    {
        public string Id { get; set; } = "";
        public string Message { get; set; } = "";
        public DateTime Timestamp { get; set; }
    }
}
