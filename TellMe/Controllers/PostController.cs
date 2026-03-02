using Microsoft.AspNetCore.Mvc;
using TellMe.Services;

namespace TellMe.Controllers
{
    [ApiController]
    [Route("facebook")]
    public class PostController : ControllerBase
    {
        private readonly PostService _postService;

        public PostController(PostService postService)
        {
            _postService = postService;
        }

        [HttpPost("publish-multiple-photos")]
        public async Task<IActionResult> CreatePostWithMultiplePhotos(
            [FromForm] string content,
            [FromForm] List<IFormFile> files)
        {
            if (files == null || files.Count == 0)
            {
                return BadRequest(new { message = "Bạn chưa chọn ảnh nào để upload!" });
            }

            if (files.Count > 10)
            {
                return BadRequest(new { message = "Chỉ được phép upload tối đa 10 ảnh!" });
            }

            var result = await _postService.PublishMultiplePhotosAsync(content, files);
            return Ok(result);
        }
    }
}