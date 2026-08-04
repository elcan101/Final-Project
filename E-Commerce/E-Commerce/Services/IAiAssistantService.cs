namespace E_Commerce.Services
{
    public class AiChatTurn
    {
        public string Role { get; set; } = "user"; 
        public string Text { get; set; } = "";
    }

    public class AiRecommendedBook
    {
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
