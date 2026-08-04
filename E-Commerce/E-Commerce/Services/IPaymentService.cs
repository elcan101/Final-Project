namespace E_Commerce.Services
{
    public class PaymentResult
    {
        public bool Success { get; set; }
        public string ChargeId { get; set; } = null!;
        public string? ErrorMessage { get; set; }
    }

    public interface IPaymentService
    {
        Task<(string token, string brand, string last4)> TokenizeCardAsync(string cardNumber, string expiry, string cvc);

        Task<PaymentResult> ChargeAsync(string userId, decimal amount, string description, string? stripeToken = null);
    }
}
