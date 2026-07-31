namespace E_Commerce.Services
{
    public class DepotOptions
    {
        public string Name { get; set; } = "Kitab Deposu — 28 May, Dilarə Əliyeva küçəsi 239";
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public decimal BaseFee { get; set; } = 1.50m;
        public decimal PerKmFee { get; set; } = 0.35m;
        public decimal MinFee { get; set; } = 2.00m;
        public decimal MaxFee { get; set; } = 15.00m;
    }

    // Depodan (kitabların saxlandığı anbar) çatdırılma ünvanına qədər olan məsafəyə
    // görə çatdırılma haqqını hesablayır. Bu haqq kuryerin balansına köçürülür.
    public class DeliveryPricingService
    {
        private readonly DepotOptions _depot;

        public DeliveryPricingService(IConfiguration config)
        {
            _depot = config.GetSection("Depot").Get<DepotOptions>() ?? new DepotOptions();
        }

        public DepotOptions Depot => _depot;

        // Haversine düsturu ilə iki koordinat arasındakı məsafə (km)
        public static double DistanceKm(double lat1, double lng1, double lat2, double lng2)
        {
            const double earthRadiusKm = 6371.0;
            double dLat = ToRadians(lat2 - lat1);
            double dLng = ToRadians(lng2 - lng1);

            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                       Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return earthRadiusKm * c;
        }

        private static double ToRadians(double deg) => deg * Math.PI / 180.0;

        // Çatdırılma haqqını hesablayır: sabit baza + hər km üçün əlavə haqq, min/max limitlər daxilində
        public decimal CalculateDeliveryFee(double? deliveryLat, double? deliveryLng, out double distanceKm)
        {
            if (deliveryLat == null || deliveryLng == null)
            {
                distanceKm = 0;
                return _depot.BaseFee;
            }

            distanceKm = DistanceKm(_depot.Latitude, _depot.Longitude, deliveryLat.Value, deliveryLng.Value);

            var fee = _depot.BaseFee + (decimal)distanceKm * _depot.PerKmFee;
            fee = Math.Round(fee, 2);

            if (fee < _depot.MinFee) fee = _depot.MinFee;
            if (fee > _depot.MaxFee) fee = _depot.MaxFee;

            return fee;
        }
    }
}
