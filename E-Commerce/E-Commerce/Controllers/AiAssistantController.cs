using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using E_Commerce.Services;

namespace E_Commerce.Controllers
{
    // Sayt daxili "Kitab AI" köməkçisinin backend endpoint-i.
    // Frontend (chat widget, bax wwwroot/js/ai-assistant.js) buraya istifadəçinin
    // yazdığı mesajı və qısa söhbət tarixçəsini göndərir, cavab olaraq AI-nın mətni
    // və (əgər varsa) tövsiyə etdiyi kitabların kartlarını alır.
    [AllowAnonymous] // qonaq istifadəçilər də (giriş etmədən) AI-dan istifadə edə bilsin
    public class AiAssistantController : Controller
    {
        private readonly IAiAssistantService _aiService;

        public AiAssistantController(IAiAssistantService aiService)
        {
            _aiService = aiService;
        }

        public class AskRequest
        {
            public string Message { get; set; } = "";
            public List<AiChatTurn>? History { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> Ask([FromBody] AskRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new { error = "Mesaj boş ola bilməz." });
            }

            // sürüşmə/sui-istifadəni azaltmaq üçün sadə uzunluq limiti
            var message = request.Message.Length > 500 ? request.Message[..500] : request.Message;

            var response = await _aiService.AskAsync(message, request.History ?? new List<AiChatTurn>());

            return Json(new
            {
                reply = response.Reply,
                books = response.Books.Select(b => new
                {
                    b.Id,
                    b.Title,
                    b.Author,
                    price = b.Price,
                    imageUrl = b.ImageUrl,
                    rating = b.Rating,
                    isSecondHand = b.IsSecondHand,
                    url = Url.Action("Details", "Product", new { id = b.Id })
                })
            });
        }
    }
}
