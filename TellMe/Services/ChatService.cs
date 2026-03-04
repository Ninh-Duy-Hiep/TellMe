using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using TellMe.Data;
using TellMe.Hubs;
using TellMe.Models;

namespace TellMe.Services
{
    public class ChatService
    {
        private readonly AppDbContext _context;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly IHubContext<ChatHub> _hubContext;

        public ChatService(AppDbContext context, HttpClient httpClient, IConfiguration configuration, IHubContext<ChatHub> hubContext)
        {
            _context = context;
            _httpClient = httpClient;
            _configuration = configuration;
            _hubContext = hubContext;
        }

        public async Task<FacebookMessage> SaveIncomingMessageAsync(string messageId, string senderPsid, string text, string? replyToId = null, string? forwardedMessageId = null)
        {
            var newMessage = new FacebookMessage
            {
                Id = messageId,
                SenderPsid = senderPsid,
                Text = text,
                IsReply = false,
                ReplyToId = replyToId,
                ForwardedMessageId = forwardedMessageId
            };

            _context.FacebookMessages.Add(newMessage);
            await _context.SaveChangesAsync();

            await _hubContext.Clients.All.SendAsync("ReceiveNewMessage", newMessage);

            return newMessage;
        }

        public async Task<FacebookMessage> SaveOutgoingMessageAsync(string messageId, string recipientPsid, string text, string? replyToId = null, string? forwardedMessageId = null)
        {
            var newMessage = new FacebookMessage
            {
                Id = messageId,
                SenderPsid = recipientPsid,
                Text = text,
                IsReply = true,
                ReplyToId = replyToId,
                ForwardedMessageId = forwardedMessageId
            };

            _context.FacebookMessages.Add(newMessage);
            await _context.SaveChangesAsync();

            await _hubContext.Clients.All.SendAsync("ReceiveNewMessage", newMessage);

            return newMessage;
        }

        public async Task<(bool success, string? messageId, string? error)> SendMessageToFacebookAsync(string recipientPsid, string text, string? replyToId = null)
        {
            var pageId = _configuration["Facebook:PageID"];
            var pageAccessToken = _configuration["Facebook:PageToken"];
            var url = $"https://graph.facebook.com/v19.0/{pageId}/messages?access_token={pageAccessToken}";

            object payload;

            if (!string.IsNullOrEmpty(replyToId))
            {
                payload = new
                {
                    recipient = new { id = recipientPsid },
                    messaging_type = "RESPONSE",
                    message = new { text = text },
                    reply_to = new { mid = replyToId }
                };
            }
            else
            {
                payload = new
                {
                    recipient = new { id = recipientPsid },
                    messaging_type = "RESPONSE",
                    message = new { text = text }
                };
            }

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(url, content);
                var responseData = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[System -> FB] Da gui thanh cong den ID {recipientPsid}");
                    using (JsonDocument doc = JsonDocument.Parse(responseData))
                    {
                        var root = doc.RootElement;
                        var messageId = root.TryGetProperty("message_id", out var mid) ? mid.GetString() : null;
                        return (true, messageId, null);
                    }
                }
                else
                {
                    Console.WriteLine($"Lỗi gửi tin nhắn FB: {responseData}");
                    return (false, null, responseData);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi gửi tin nhắn FB: {ex.Message}");
                return (false, null, ex.Message);
            }
        }

        public async Task<object> GetConversationsAsync(int page = 1, int limit = 20)
        {
            var query = _context.FacebookMessages
                .GroupBy(m => m.SenderPsid)
                .Select(g => new
                {
                    SenderPsid = g.Key,
                    TotalMessages = g.Count(),
                    LastInteractionAt = g.Max(m => m.CreatedAt)
                });

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)limit);

            var conversations = await query
                .OrderByDescending(c => c.LastInteractionAt)
                .Skip((page - 1) * limit)
                .Take(limit)
                .ToListAsync();

            var lastMessageDict = new Dictionary<string, FacebookMessage>();
            foreach (var conv in conversations)
            {
                var msg = await _context.FacebookMessages
                    .Where(m => m.SenderPsid == conv.SenderPsid)
                    .OrderByDescending(m => m.CreatedAt)
                    .FirstOrDefaultAsync();
                
                if (msg != null)
                {
                    lastMessageDict[conv.SenderPsid] = msg;
                }
            }

            var pageAccessToken = _configuration["Facebook:PageToken"];

            var enrichmentTasks = conversations.Select(async c =>
            {
                var profile = await GetFacebookUserProfileAsync(c.SenderPsid, pageAccessToken);
                
                var lastMsg = lastMessageDict.ContainsKey(c.SenderPsid) ? lastMessageDict[c.SenderPsid] : null;

                return new
                {
                    SenderPsid = c.SenderPsid,
                    CustomerName = profile.CustomerName,
                    FirstName = profile.FirstName,
                    LastName = profile.LastName,
                    AvatarUrl = profile.AvatarUrl,
                    TotalMessages = c.TotalMessages,
                    LastInteractionAt = c.LastInteractionAt,
                    LastMessageText = lastMsg?.Text ?? "",
                    LastMessageIsReply = lastMsg?.IsReply ?? false
                };
            });

            var enrichedConversations = await Task.WhenAll(enrichmentTasks);

            return new
            {
                data = enrichedConversations,
                pagination = new { totalItems, totalPages, currentPage = page, limit }
            };
        }

        public async Task<object> GetMessageHistoryAsync(string psid, int page = 1, int limit = 20)
        {
            var query = _context.FacebookMessages.Where(m => m.SenderPsid == psid);

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)limit);

            var messages = await query
                .OrderByDescending(m => m.CreatedAt)
                .Skip((page - 1) * limit)
                .Take(limit)
                .ToListAsync();

            messages.Reverse();

            var mappedData = messages.Select(m => new {
                id = m.Id,
                senderPsid = m.SenderPsid,
                text = m.Text,
                isReply = m.IsReply,
                createdAt = m.CreatedAt,
                replyToId = m.ReplyToId,
                forwardedMessageId = m.ForwardedMessageId
            });

            var pageAccessToken = _configuration["Facebook:PageToken"];
            var profile = await GetFacebookUserProfileAsync(psid, pageAccessToken);

            return new
            {
                customer = new 
                {
                    psid = psid,
                    name = profile.CustomerName,
                    avatarUrl = profile.AvatarUrl
                },
                data = mappedData,
                meta = new { totalItems, totalPages, currentPage = page, limit }
            };
        }

        private async Task<(string FirstName, string LastName, string CustomerName, string AvatarUrl)> GetFacebookUserProfileAsync(string psid, string pageAccessToken)
        {
            string customerName = "";
            string avatarUrl = "";
            string firstName = "";
            string lastName = "";

            var url = $"https://graph.facebook.com/{psid}?fields=first_name,last_name,profile_pic&access_token={pageAccessToken}";

            try
            {
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();

                    using (JsonDocument doc = JsonDocument.Parse(jsonString))
                    {
                        var root = doc.RootElement;

                        firstName = root.TryGetProperty("first_name", out var fn) ? (fn.GetString() ?? "") : "";
                        lastName = root.TryGetProperty("last_name", out var ln) ? (ln.GetString() ?? "") : "";

                        customerName = $"{lastName} {firstName}".Trim();

                        if (root.TryGetProperty("profile_pic", out var pic))
                        {
                            avatarUrl = pic.GetString() ?? avatarUrl;
                        }
                    }
                }
                else 
                {
                    customerName = psid;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi lấy thông tin FB cho PSID {psid}: {ex.Message}");
                customerName = psid;
            }

            if (string.IsNullOrEmpty(customerName))
            {
                customerName = "Khách hàng";
            }

            return (firstName, lastName, customerName, avatarUrl);
        }
    }
}