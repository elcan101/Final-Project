namespace E_Commerce.Services
{
    // Sadə söhbət tarixçəsi elementi (frontend hər sorğuda əvvəlki 6-8 mesajı göndərir)
    public class AiChatTurn
    {
        public string Role { get; set; } = "user"; // "user" və ya "assistant"
        public string Text { get; set; } = "";
    

    // AI-nın qaytardığı tövsiyə olunan kitab (Product cədvəlindən götürülüb frontend-ə göndərilir)
    public class AiRecommendedBook
    
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string? Author { get; set; }
        public decimal Price { get; set; }
        public string? ImageUrl { get; set; }
        public double Rating { get; set; }
        public bool IsSecondHand { get; set; }
    }

    public class AiAssistantResponse
    {
        public string Reply { get; set; } = "";
        public List<AiRecommendedBook> Books { get; set; } = new();
    }

    public interface IAiAssistantService
    {
        Task<AiAssistantResponse> AskAsync(string userMessage, List<AiChatTurn> history);
    }
}
