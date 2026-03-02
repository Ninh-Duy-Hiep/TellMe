using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TellMe.DTOs;

namespace TellMe.Services
{
    public class PostService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public PostService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<object> PublishMultiplePhotosAsync(string content, List<IFormFile> files)
        {
            var pageAccessToken = _configuration["Facebook:PageToken"];
            var pageId = _configuration["Facebook:PageID"];
            var attachedMediaIds = new List<string>();

            Console.WriteLine($"Bắt đầu upload {files.Count} ảnh lên Facebook...");

            try
            {
                foreach (var file in files)
                {
                    using var formData = new MultipartFormDataContent();

                    formData.Add(new StringContent(pageAccessToken!), "access_token");
                    formData.Add(new StringContent("false"), "published");

                    var fileStreamContent = new StreamContent(file.OpenReadStream());
                    fileStreamContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
                    formData.Add(fileStreamContent, "source", file.FileName);

                    var uploadUrl = $"https://graph.facebook.com/v19.0/{pageId}/photos";

                    var uploadRes = await _httpClient.PostAsync(uploadUrl, formData);
                    var responseString = await uploadRes.Content.ReadAsStringAsync();

                    if (!uploadRes.IsSuccessStatusCode)
                    {
                        throw new Exception($"Lỗi upload ảnh {file.FileName}: {responseString}");
                    }

                    var photoData = JsonSerializer.Deserialize<FacebookPhotoUploadResponse>(responseString);
                    if (photoData?.Id != null)
                    {
                        attachedMediaIds.Add(photoData.Id);
                        Console.WriteLine($"- Đã upload xong ảnh: {file.FileName} (ID: {photoData.Id})");
                    }
                }

                Console.WriteLine("Đang xuất bản bài viết chính...");
                var feedUrl = $"https://graph.facebook.com/v19.0/{pageId}/feed";

                var attachedMedia = attachedMediaIds.Select(id => new { media_fbid = id }).ToList();

                var feedPayload = new
                {
                    access_token = pageAccessToken,
                    message = content,
                    attached_media = attachedMedia
                };

                var jsonPayload = JsonSerializer.Serialize(feedPayload);
                var stringContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var feedRes = await _httpClient.PostAsync(feedUrl, stringContent);
                var feedResponseString = await feedRes.Content.ReadAsStringAsync();

                if (!feedRes.IsSuccessStatusCode)
                {
                    throw new Exception($"Lỗi đăng bài viết: {feedResponseString}");
                }

                var feedData = JsonSerializer.Deserialize<FacebookFeedResponse>(feedResponseString);
                Console.WriteLine($"[Hệ thống -> FB] Đã đăng bài nhiều ảnh thành công! ID: {feedData?.Id}");

                return new
                {
                    success = true,
                    postId = feedData?.Id,
                    mediaIds = attachedMediaIds
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi đăng nhiều ảnh: {ex.Message}");
                return new { success = false, error = ex.Message };
            }
        }
    }
}