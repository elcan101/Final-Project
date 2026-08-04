using System.Globalization;

namespace E_Commerce.Services
{
    public static class CardValidationHelper
    {
        public static bool TryValidate(string? cardNumber, string? expiry, string? cvc, out string error)
        {
            var digits = new string((cardNumber ?? "").Where(char.IsDigit).ToArray());
            if (digits.Length != 16)
            {
                error = "Kart nömrəsi düzgün deyil — mütləq 16 rəqəmdən ibarət olmalıdır.";
                return false;
            }

            var expiryDigits = new string((expiry ?? "").Where(char.IsDigit).ToArray());
            if (expiryDigits.Length != 4)
            {
                error = "Son istifadə tarixi AA/İİ formatında (məs: 09/28) daxil edilməlidir.";
                return false;
            }

            var month = int.Parse(expiryDigits.Substring(0, 2), CultureInfo.InvariantCulture);
            var yearShort = int.Parse(expiryDigits.Substring(2, 2), CultureInfo.InvariantCulture);

            if (month < 1 || month > 12)
            {
                error = "Son istifadə tarixindəki ay 01 ilə 12 arasında olmalıdır.";
                return false;
            }

            var fullYear = 2000 + yearShort;
            var expiryLastDay = new DateTime(fullYear, month, 1).AddMonths(1).AddDays(-1);

            if (expiryLastDay < DateTime.Now.Date)
            {
                error = "Kartın son istifadə tarixi artıq keçib.";
                return false;
            }

            if (fullYear > DateTime.Now.Year + 15)
            {
                error = "Son istifadə tarixi düzgün görünmür.";
                return false;
            }

            var cvcDigits = new string((cvc ?? "").Where(char.IsDigit).ToArray());
            if (cvcDigits.Length < 3 || cvcDigits.Length > 4)
            {
                error = "CVV 3 və ya 4 rəqəmdən ibarət olmalıdır.";
                return false;
            }

            error = "";
            return true;
        }
    }
}
