using System.Text.Json.Serialization;

namespace TellMe.DTOs
{
    public class FacebookPhotoUploadResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }

    public class FacebookFeedResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }
}