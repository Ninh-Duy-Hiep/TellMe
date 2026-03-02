using System.Text.Json.Serialization;

namespace TellMe.DTOs
{

    public class FacebookWebhookBody
    {
        [JsonPropertyName("object")]
        public string Object { get; set; } = string.Empty;

        [JsonPropertyName("entry")]
        public List<WebhookEntry> Entry { get; set; } = new();
    }

    public class WebhookEntry
    {
        [JsonPropertyName("messaging")]
        public List<WebhookEvent> Messaging { get; set; } = new();
    }

    public class WebhookEvent
    {
        [JsonPropertyName("sender")]
        public WebhookSender Sender { get; set; } = new();

        [JsonPropertyName("recipient")]
        public WebhookRecipient Recipient { get; set; } = new();

        [JsonPropertyName("message")]
        public WebhookMessage? Message { get; set; }
    }

    public class WebhookSender
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
    }

    public class WebhookRecipient
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
    }

    public class WebhookMessage
    {
        [JsonPropertyName("mid")]
        public string? Mid { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("is_echo")]
        public bool IsEcho { get; set; }
    }

    public class SendMessageRequest
    {
        public string Psid { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }
}