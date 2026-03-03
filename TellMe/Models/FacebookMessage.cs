using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TellMe.Models
{
    public class FacebookMessage
    {
        [Key]
        public string Id { get; set; } = string.Empty;

        public string SenderPsid { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;

        public bool IsReply { get; set; } = false;

        public string? ReplyToId { get; set; }

        public string? ForwardedMessageId { get; set; }

        [Column(TypeName = "timestamp")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}