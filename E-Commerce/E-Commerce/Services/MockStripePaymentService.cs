namespace E_Commerce.Services
{
    
    public class MockStripePaymentService : IPaymentService
    {
        private readonly ILogger<MockStripePaymentService> _logger;

        public MockStripePaymentService(ILogger<MockStripePaymentService> logger)
        {
            _logger = logger;
        }

        public Task<(string token, string brand, string last4)> TokenizeCardAsync(string cardNumber, string expiry, string cvc)
        {
            var cleaned = new string((cardNumber ?? "").Where(char.IsDigit).ToArray());
            var last4 = cleaned.Length >= 4 ? cleaned[^4..] : "0000";

            var brand = cleaned.StartsWith("4") ? "Visa"
                : cleaned.StartsWith("5") ? "MasterCard"
                : "Card";

            var token = $"tok_mock_{Guid.NewGuid():N}".Substring(0, 24);

            _logger.LogInformation("[MockStripe] Kart tokenləşdirildi: {Brand} ****{Last4}", brand, last4);
            return Task.FromResult((token, brand, last4));
        }

        public Task<PaymentResult> ChargeAsync(string userId, decimal amount, string description, string? stripeToken = null)
        {
            _logger.LogInformation("[MockStripe] Ödəniş: {Amount} AZN — {Description} (İstifadəçi: {UserId})", amount, description, userId);

            return Task.FromResult(new PaymentResult
            {
                Success = true,
                ChargeId = $"ch_mock_{Guid.NewGuid():N}".Substring(0, 24),
            });
        }
    }
}
