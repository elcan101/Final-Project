namespace E_Commerce.Services
{
    // ============================================================================
    // TEST REJİMİ: Real SMTP server olmadan işləyən saxta (mock) e-poçt servisi.
    // Mesajlar göndərilmir, sadəcə loglanır ki, layihəni SMTP hesabı olmadan da
    // test etmək mümkün olsun.
    //
    // Production-a keçid üçün:
    //   1) appsettings.json-a "Smtp:Host", "Smtp:Port", "Smtp:User", "Smtp:Password" əlavə et
    //   2) Bu klası SmtpEmailService kimi yenidən yaz (System.Net.Mail.SmtpClient və ya
    //      MailKit istifadə edərək) və faktiki e-poçt göndər
    //   3) Program.cs-də IEmailService qeydiyyatını dəyişdir:
    //        builder.Services.AddScoped<IEmailService, SmtpEmailService>();
    // ============================================================================
    public class MockEmailService : IEmailService
    {
        private readonly ILogger<MockEmailService> _logger;

        public MockEmailService(ILogger<MockEmailService> logger)
        {
            _logger = logger;
        }

        public Task SendAsync(string toEmail, string subject, string htmlBody)
        {
            // Test rejimində real e-poçt göndərilmir, yalnız loga yazılır
            _logger.LogInformation("[MockEmail] Kimə: {ToEmail} | Mövzu: {Subject}\n{Body}", toEmail, subject, htmlBody);
            return Task.CompletedTask;
        }
    }
}
