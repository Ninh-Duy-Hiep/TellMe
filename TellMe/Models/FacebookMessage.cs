using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TellMe.Models
{
    public class FacebookMessage
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string SenderPsid { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;

        public bool IsReply { get; set; } = false;

        [Column(TypeName = "timestamp")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}