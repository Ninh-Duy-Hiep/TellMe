using Microsoft.AspNetCore.Mvc;
using TellMe.DTOs;
using TellMe.Models;
using TellMe.Services;

namespace TellMe.Controllers
{
    [ApiController]
    [Route("webhook")]
    public class ChatController : ControllerBase
    {
        private readonly ChatService _chatService;
        private readonly IConfiguration _configuration;

        public ChatController(ChatService chatService, IConfiguration configuration)
        {
            _chatService = chatService;
            _configuration = configuration;
        }

        [HttpGet]
        public IActionResult VerifyWebhook(
            [FromQuery(Name = "hub.mode")] string? mode,
            [FromQuery(Name = "hub.verify_token")] string? token,
            [FromQuery(Name = "hub.challenge")] string? challenge)
        {
            var verifyToken = _configuration["Facebook:VerifyToken"];

            if (!string.IsNullOrEmpty(mode) && !string.IsNullOrEmpty(token))
            {
                if (mode == "subscribe" && token == verifyToken)
                {
                    Console.WriteLine("WEBHOOK_VERIFIED");
                    return Content(challenge, "text/plain");
                }
                else
                {
                    return Forbid();
                }
            }
            return BadRequest();
        }

        [HttpPost]
        public async Task<IActionResult> ReceiveMessage([FromBody] FacebookWebhookBody body)
        {
            Console.WriteLine("--- REQUEST POST CUA FACEBOOK ---");

            if (body.Object == "page")
            {
                foreach (var entry in body.Entry)
                {
                    if (entry.Messaging != null && entry.Messaging.Count > 0)
                    {
                        var webhookEvent = entry.Messaging[0];
                        var senderPsid = webhookEvent.Sender.Id;
                        var recipientPsid = webhookEvent.Recipient?.Id;

                        if (webhookEvent.Message != null && webhookEvent.Message.IsEcho)
                        {
                            var messageText = webhookEvent.Message.Text;
                            if (!string.IsNullOrEmpty(recipientPsid) && !string.IsNullOrEmpty(messageText))
                            {
                                Console.WriteLine($"[FB Chat Echo] Page vua nhan cho {recipientPsid}: {messageText}");
                                await _chatService.SaveOutgoingMessageAsync(
                                    webhookEvent.Message.Mid ?? $"echo_{Guid.NewGuid()}",
                                    recipientPsid, 
                                    messageText,
                                    webhookEvent.Message.ReplyTo?.Mid,
                                    null
                                );
                            }
                        }
                        else if (webhookEvent.Message != null && !string.IsNullOrEmpty(webhookEvent.Message.Text))
                        {
                            var messageText = webhookEvent.Message.Text;
                            Console.WriteLine($"[FB Chat] Nhan tu {senderPsid}: {messageText}");
                            await _chatService.SaveIncomingMessageAsync(
                                webhookEvent.Message.Mid ?? $"msg_{Guid.NewGuid()}",
                                senderPsid, 
                                messageText,
                                webhookEvent.Message.ReplyTo?.Mid,
                                null
                            );
                        }
                    }
                }
                return Content("EVENT_RECEIVED", "text/plain");
            }
            else
            {
                return NotFound();
            }
        }

        [HttpPost("send")]
        public async Task<IActionResult> ReplyToCustomer([FromBody] SendMessageRequest request)
        {
            var result = await _chatService.SendMessageToFacebookAsync(request.Psid, request.Text, request.ReplyToId);
            
            if (result.success && !string.IsNullOrEmpty(result.messageId))
            {
                await _chatService.SaveOutgoingMessageAsync(
                    result.messageId,
                    request.Psid, 
                    request.Text, 
                    request.ReplyToId, 
                    request.ForwardedMessageId
                );
            }

            return Ok(new { success = result.success, error = result.error });
        }

        [HttpGet("conversations")]
        public async Task<IActionResult> GetConversations([FromQuery] int page = 1, [FromQuery] int limit = 20)
        {
            try
            {
                var result = await _chatService.GetConversationsAsync(page, limit);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi lấy danh sách chat", error = ex.Message });
            }
        }

        [HttpGet("history/{psid}")]
        public async Task<IActionResult> GetChatHistory(string psid, [FromQuery] int page = 1, [FromQuery] int limit = 20)
        {
            try
            {
                var result = await _chatService.GetMessageHistoryAsync(psid, page, limit);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi lấy lịch sử chat", error = ex.Message });
            }
        }
    }
}