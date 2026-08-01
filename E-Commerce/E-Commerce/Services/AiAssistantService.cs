using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using E_Commerce.Data;
using Microsoft.EntityFrameworkCore;

namespace E_Commerce.Services
{
    // ============================================================================
    // SAYT DAXİLİ AI KÖMƏKÇİ ("Kitab AI")
    //
    // Nə edir:
    //   1) Kitab tövsiyəsi: istifadəçi "əvvəli xoşbəxt, sonu kədərli romanlar göstər"
    //      kimi sərbəst dildə sual verəndə, hazırkı kataloqdakı kitabların
    //      Description sahələrini AI-ya göndəririk, AI məzmuna görə uyğun olanları seçir.
    //   2) Sayt haqqında suallar (çatdırılma müddəti, qaytarma şərtləri, aktiv
    //      kampaniyalar, ikinci əl kitab necə yerləşdirilir və s.) — appsettings.json
    //      daxilindəki "SiteFaq" bölməsindən (real, admin tərəfindən yazılan faktlardan)
    //      cavablandırır ki, AI heç nə "uydurmasın".
    //
    // Niyə belə qurulub:
    //   Hər sorğuda bütün kataloq (qısaldılmış description ilə) system promptuna
    //   ötürülür — beləliklə tövsiyələr HƏMİŞə anbarın hazırkı vəziyyətinə (qiymət,
    //   stok, yeni əlavə olunan kitablar) uyğun olur, köhnəlmiş/uydurma cavab riski
    //   olmur. Kataloq çox böyüyəndə (min kitabdan çox) bu yanaşma baha başa gələr —
    //   o zaman description-ları əvvəlcədən vektor (embedding) bazasına köçürüb,
    //   yalnız açar sözlərə uyğun olan hissəni AI-ya göndərmək lazım gələcək.
    //
    // Production üçün əlavə tövsiyələr:
    //   - Bu endpoint-ə sadə "rate limiting" (məs. IP başına dəqiqədə 10 sorğu) əlavə edin,
    //     əks halda kimsə botla saytınızın AI xərcini şişirdə bilər.
    //   - API açarını appsettings.json-da SAXLAMAYIN (development üçün rahatlıqdır),
    //     production-da mütləq environment variable və ya "dotnet user-secrets" istifadə edin.
    // ============================================================================
    public class AiAssistantService : IAiAssistantService
    {
        private readonly HttpClient _http;
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;
        private readonly ILogger<AiAssistantService> _logger;

        // Kataloqdan AI-ya göndərilən maksimum kitab sayı (token xərcini məhdudlaşdırmaq üçün)
        private const int MaxCatalogItems = 300;
        // Hər kitabın description-undan neçə simvol göndərilsin
        private const int DescriptionSnippetLength = 350;

        public AiAssistantService(HttpClient http, AppDbContext db, IConfiguration config, ILogger<AiAssistantService> logger)
        {
            _http = http;
            _db = db;
            _config = config;
            _logger = logger;
        }

        public async Task<AiAssistantResponse> AskAsync(string userMessage, List<AiChatTurn> history)
        {
            var apiKey = _config["Anthropic:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("AI köməkçi çağırıldı, amma Anthropic:ApiKey appsettings.json-da doldurulmayıb.");
                return new AiAssistantResponse
                {
                    Reply = "Üzr istəyirəm, AI köməkçi hazırda müvəqqəti əlçatan deyil. Zəhmət olmasa bir az sonra yenidən cəhd edin və ya bizimlə birbaşa əlaqə saxlayın."
                };
            }

            var catalog = await BuildCatalogSnippetAsync();
            var systemPrompt = BuildSystemPrompt(catalog);

            var messages = new List<object>();
            // son 8 mesajı (4 istifadəçi + 4 cavab) tarixçə kimi əlavə edirik ki, AI kontekst itirməsin
            foreach (var turn in history.TakeLast(8))
            {
                messages.Add(new { role = turn.Role == "assistant" ? "assistant" : "user", content = turn.Text });
            }
            messages.Add(new { role = "user", content = userMessage });

            var requestBody = new
            {
                model = _config["Anthropic:Model"] ?? "claude-sonnet-5",
                max_tokens = 800,
                system = systemPrompt,
                messages
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
            req.Headers.Add("x-api-key", apiKey);
            req.Headers.Add("anthropic-version", "2023-06-01");
            req.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            string rawText;
            try
            {
                var res = await _http.SendAsync(req);
                var body = await res.Content.ReadAsStringAsync();

                if (!res.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Anthropic API xətası: {Status} | {Body}", res.StatusCode, body);
                    return new AiAssistantResponse { Reply = "Üzr istəyirəm, AI köməkçi hazırda cavab verə bilmir. Bir az sonra yenidən cəhd edin." };
                }

                using var doc = JsonDocument.Parse(body);
                rawText = doc.RootElement
                    .GetProperty("content")[0]
                    .GetProperty("text")
                    .GetString() ?? "";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI köməkçiyə qoşularkən xəta baş verdi");
                return new AiAssistantResponse { Reply = "Üzr istəyirəm, AI köməkçiyə qoşulmaq mümkün olmadı. İnternet bağlantısını yoxlayıb yenidən cəhd edin." };
            }

            return await ParseResponseAsync(rawText);
        }

        // AI cavabının sonundakı gizli <!--BOOKS:1,5,9--> blokunu tapır, oradan kitab ID-lərini
        // çıxarır, DB-dən HƏMİŞƏ təzə (qiymət/stok/şəkil) məlumatla product kartlarını qurur.
        private async Task<AiAssistantResponse> ParseResponseAsync(string rawText)
        {
            var result = new AiAssistantResponse();
            var match = Regex.Match(rawText, @"<!--\s*BOOKS:([0-9,\s]*)\s*-->");

            var cleanText = match.Success ? rawText.Remove(match.Index, match.Length).Trim() : rawText.Trim();
            result.Reply = cleanText;

            if (match.Success)
            {
                var ids = match.Groups[1].Value
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(s => int.TryParse(s, out var n) ? n : (int?)null)
                    .Where(n => n.HasValue)
                    .Select(n => n!.Value)
                    .Distinct()
                    .Take(8)
                    .ToList();

                if (ids.Count > 0)
                {
                    var products = await _db.Products
                        .Where(p => !p.IsDeleted && ids.Contains(p.Id))
                        .ToListAsync();

                    // AI-nın verdiyi sıra ilə (relevanslıq sırası) saxlayırıq
                    result.Books = ids
                        .Select(id => products.FirstOrDefault(p => p.Id == id))
                        .Where(p => p != null)
                        .Select(p => new AiRecommendedBook
                        {
                            Id = p!.Id,
                            Title = p.Title,
                            Author = p.Author,
                            Price = p.Price,
                            ImageUrl = p.ImageUrl,
                            Rating = p.Rating,
                            IsSecondHand = p.IsSecondHand
                        })
                        .ToList();
                }
            }

            return result;
        }

        private async Task<string> BuildCatalogSnippetAsync()
        {
            var products = await _db.Products
                .Where(p => !p.IsDeleted)
                .Include(p => p.Category)
                .OrderByDescending(p => p.CreatedDate) // ən yeni əlavə olunanlar əvvəldə — "son çıxanlar" sualları üçün
                .Take(MaxCatalogItems)
                .Select(p => new
                {
                    p.Id,
                    p.Title,
                    p.Author,
                    Category = p.Category.Name,
                    p.Description,
                    p.Price,
                    p.Rating,
                    p.IsSecondHand,
                    p.IsDigital,
                    p.IsAudio,
                    InStock = p.StockCount > 0,
                    p.CreatedDate
                })
                .ToListAsync();

            var sb = new StringBuilder();
            foreach (var p in products)
            {
                var desc = p.Description?.Length > DescriptionSnippetLength
                    ? p.Description[..DescriptionSnippetLength] + "…"
                    : p.Description;

                sb.Append($"#{p.Id} | \"{p.Title}\" | Müəllif: {p.Author ?? "-"} | Janr: {p.Category} | ")
                  .Append($"Qiymət: {p.Price} AZN | Reytinq: {p.Rating}/5 | ")
                  .Append($"{(p.IsSecondHand ? "İkinci əl" : "Yeni")} | {(p.InStock ? "Stokda var" : "Stokda yoxdur")} | ")
                  .Append($"Əlavə olunma tarixi: {p.CreatedDate:yyyy-MM-dd}\n")
                  .Append($"Qısa məzmun: {desc}\n\n");
            }

            return sb.ToString();
        }

        private string BuildSystemPrompt(string catalog)
        {
            var siteName = _config["SiteFaq:SiteName"] ?? "Okean Kitabevi";
            var deliveryInfo = _config["SiteFaq:DeliveryInfo"] ?? "Çatdırılma müddəti və qaydaları admin tərəfindən hələ doldurulmayıb.";
            var returnPolicy = _config["SiteFaq:ReturnPolicy"] ?? "Qaytarma/dəyişdirmə şərtləri admin tərəfindən hələ doldurulmayıb.";
            var campaigns = _config["SiteFaq:ActiveCampaigns"] ?? "Hazırda elan olunmuş xüsusi kampaniya yoxdur.";
            var usedBookSteps = _config["SiteFaq:SellUsedBookSteps"] ??
                "İstifadəçi \"Elan Ver\" bölməsindən kitabın şəklini, qiymətini və vəziyyətini daxil edərək elanı öz profilindən yerləşdirə bilər.";

            return $$"""
Sən "{{siteName}}" kitab satışı saytının daxili AI köməkçisisən. İstifadəçilərlə YALNIZ AZƏRBAYCAN dilində, səmimi və qısa danış.

SƏNİN İKİ VƏZİFƏN VAR:

1) KİTAB TÖVSİYƏSİ: İstifadəçi mövzu, janr, əhval-ruhiyyə, oxşar kitab və ya "son çıxanlar" kimi sərbəst şəkildə kitab axtarırsa, aşağıdakı KATALOQ siyahısındakı "Qısa məzmun" hissələrini diqqətlə oxuyub yalnız HƏQİQƏTƏN uyğun olan kitabları seç. Heç vaxt kataloqda olmayan kitab uydurma. Uyğun kitab tapmasan, bunu açıq şəkildə de və kataloqdakı ən yaxın alternativləri təklif et.
   Cavabının SONUNDA (istifadəçiyə görünməyəcək) bu formatda gizli bir sətir əlavə et:
   <!--BOOKS:id1,id2,id3-->
   (maksimum 6 ID, ən uyğun olan birinci). Əgər heç bir konkret kitab tövsiyə etmirsənsə, bu sətri ümumiyyətlə yazma.
   Cavab mətnində kitabların ID-lərini yazma (onlar avtomatik kartlar şəklində göstəriləcək) — sadəcə niyə uyğun olduqlarını 1-2 cümlə ilə izah et.

2) SAYT HAQQINDA SUALLAR: Çatdırılma, qaytarma/dəyişdirmə, aktiv kampaniyalar, ikinci əl kitab yerləşdirmə kimi suallara YALNIZ aşağıda verilən FAKTLARA əsaslanaraq cavab ver. Bu faktlarda olmayan şeyi UYDURMA — əgər məlumat yoxdursa, dürüst şəkildə "bu barədə dəqiq məlumatım yoxdur, dəstək xidmətinə yazın" de.

=== SAYT FAKTLARI ===
Çatdırılma: {{deliveryInfo}}
Qaytarma/Dəyişdirmə: {{returnPolicy}}
Aktiv kampaniyalar: {{campaigns}}
İkinci əl kitab necə yerləşdirilir: {{usedBookSteps}}
=== FAKTLARIN SONU ===

=== KİTAB KATALOQU (yalnız sənin üçündür, xam siyahı kimi istifadəçiyə göstərmə) ===
{{catalog}}
=== KATALOQUN SONU ===
""";
        }
    }
}
