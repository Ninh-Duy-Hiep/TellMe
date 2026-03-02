using System.ComponentModel.DataAnnotations;

namespace TellMe.Models
{
    public class FacebookMessage
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string SenderPsid { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;

        public bool IsReply { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}