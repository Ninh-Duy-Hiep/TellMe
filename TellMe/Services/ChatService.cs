using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using TellMe.Data;
using TellMe.Models;

namespace TellMe.Services
{
    public class ChatService
    {
        private readonly AppDbContext _context;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public ChatService(AppDbContext context, HttpClient httpClient, IConfiguration configuration)
        {
            _context = context;
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<FacebookMessage> SaveIncomingMessageAsync(string senderPsid, string text)
        {
            var newMessage = new FacebookMessage
            {
                SenderPsid = senderPsid,
                Text = text,
                IsReply = false
            };

            _context.FacebookMessages.Add(newMessage);
            await _context.SaveChangesAsync();

            return newMessage;
        }

        public async Task<FacebookMessage> SaveOutgoingMessageAsync(string recipientPsid, string text)
        {
            var newMessage = new FacebookMessage
            {
                SenderPsid = recipientPsid,
                Text = text,
                IsReply = true // Đánh dấu đây là tin nhắn của page gửi đi
            };

            _context.FacebookMessages.Add(newMessage);
            await _context.SaveChangesAsync();

            return newMessage;
        }

        public async Task<object> SendMessageToFacebookAsync(string recipientPsid, string text)
        {
            var pageAccessToken = _configuration["Facebook:PageToken"];
            var url = $"https://graph.facebook.com/v19.0/me/messages?access_token={pageAccessToken}";

            var payload = new
            {
                recipient = new { id = recipientPsid },
                message = new { text = text },
                messaging_type = "RESPONSE"
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(url, content);
                var responseData = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[System -> FB] Da gui thanh cong den ID {recipientPsid}");
                    return new { success = true, data = JsonSerializer.Deserialize<object>(responseData) };
                }
                else
                {
                    Console.WriteLine($"Lỗi gửi tin nhắn FB: {responseData}");
                    return new { success = false, error = responseData };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi gửi tin nhắn FB: {ex.Message}");
                return new { success = false, error = ex.Message };
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

            var pageAccessToken = _configuration["Facebook:PageToken"];

            var enrichmentTasks = conversations.Select(async c =>
            {
                string customerName = "";
                string avatarUrl = "";

                string firstName = "";
                string lastName = "";

                var url = $"https://graph.facebook.com/{c.SenderPsid}?fields=first_name,last_name,profile_pic&access_token={pageAccessToken}";

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
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Lỗi khi lấy thông tin FB cho PSID {c.SenderPsid}: {ex.Message}");
                }

                return new
                {
                    SenderPsid = c.SenderPsid,
                    CustomerName = string.IsNullOrEmpty(customerName) ? "Khách hàng" : customerName,
                    FirstName = firstName,
                    LastName = lastName,
                    AvatarUrl = avatarUrl,
                    TotalMessages = c.TotalMessages,
                    LastInteractionAt = c.LastInteractionAt
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

            return new
            {
                data = messages,
                pagination = new { totalItems, totalPages, currentPage = page, limit }
            };
        }
    }
}