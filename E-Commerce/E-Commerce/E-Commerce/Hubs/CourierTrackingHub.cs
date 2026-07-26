using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using E_Commerce.Data;
using E_Commerce.Models;

namespace E_Commerce.Hubs
{
    // ============================================================================
    // Real-time kuryer izləmə + "SignalR broadcast alqoritmi":
    // Sifariş hazır olduqda bütün boşda (idle) kuryerlərə siqnal göndərilir.
    // İlk təsdiqləyən kuryer sifarişi öz üzərinə götürür — digərlərinə "artıq
    // götürülüb" bildirişi gedir. Yarış vəziyyətinin (race condition) qarşısı
    // DB-də şərtli UPDATE (WHERE CourierProfileId IS NULL) ilə alınır.
    // ============================================================================
    public class CourierTrackingHub : Hub
    {
        private const string IdleCouriersGroup = "idle-couriers";
        private readonly AppDbContext _context;

        public CourierTrackingHub(AppDbContext context)
        {
            _context = context;
        }

        // Kuryer "boşdayam" statusuna keçəndə çağırır — yeni sifariş siqnallarını almaq üçün qoşulur
        public async Task JoinIdlePool(int courierProfileId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, IdleCouriersGroup);

            var courier = await _context.CourierProfiles.FindAsync(courierProfileId);
            if (courier != null)
            {
                courier.IsAvailable = true;
                await _context.SaveChangesAsync();
            }
        }

        public async Task LeaveIdlePool(int courierProfileId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, IdleCouriersGroup);

            var courier = await _context.CourierProfiles.FindAsync(courierProfileId);
            if (courier != null)
            {
                courier.IsAvailable = false;
                await _context.SaveChangesAsync();
            }
        }

        // Müştəri (və ya kuryer) sifariş izləmə səhifəsində bu qrupa qoşulur
        public async Task JoinOrderGroup(int orderId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, OrderGroupName(orderId));
        }

        // Kuryer canlı GPS koordinatlarını göndərir; müştərinin xəritəsi anlıq yenilənir
        public async Task UpdateLocation(int orderId, double lat, double lng)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null) return;

            // Sifariş hələ heç bir kuryerə təyin olunmayıbsa (qəbul edilməyibsə),
            // konum yeniləməsini rədd edirik — kuryer yalnız "Götür" düyməsini basdıqdan sonra paylaşa bilər
            if (order.CourierProfileId == null || order.Status != "Kuryerdədir")
                return;

            order.CourierLatitude = lat;
            order.CourierLongitude = lng;
            order.LastLocationUpdate = DateTime.Now;
            await _context.SaveChangesAsync();

            await Clients.Group(OrderGroupName(orderId)).SendAsync("LocationUpdated", new { orderId, lat, lng });
        }

        // İlk təsdiqləyən kuryer qazanır — atomik şərtli UPDATE ilə
        public async Task AcceptOrder(int orderId, int courierProfileId)
        {
            var affected = await _context.Orders
                .Where(o => o.Id == orderId && o.CourierProfileId == null && o.Status == "Hazırdır")
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(o => o.CourierProfileId, courierProfileId)
                    .SetProperty(o => o.Status, "Kuryerdədir"));

            if (affected == 0)
            {
                // Başqa kuryer artıq götürüb
                await Clients.Caller.SendAsync("OrderAlreadyTaken", orderId);
                return;
            }

            var courier = await _context.CourierProfiles.FindAsync(courierProfileId);
            var order = await _context.Orders.FindAsync(orderId);

            // Digər boşda kuryerlərə bildir ki, bu sifariş siyahıdan çıxsın
            await Clients.Group(IdleCouriersGroup).SendAsync("OrderTaken", orderId);

            // Müştəriyə kuryerin təyin olunduğunu bildir
            await Clients.Group(OrderGroupName(orderId))
                .SendAsync("CourierAssigned", new { orderId, courierName = courier?.VehicleType ?? "Kuryer" });

            // Götürən kuryerə birbaşa bildir ki, çatdırılma/izləmə səhifəsinə keçsin
            await Clients.Caller.SendAsync("OrderAccepted", new { orderId });

            // Sayt-daxili bildirişlər: kuryerə "sifarişi götürdünüz", müştəriyə "kuryer təyin olundu"
            if (courier != null)
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = courier.CourierId,
                    Title = "Yeni sifariş götürdünüz",
                    Message = $"#{orderId} nömrəli sifarişi götürdünüz — çatdırılma haqqının 70%-i olan {order?.CourierEarning.ToString("0.00") ?? "0.00"} AZN çatdırıldıqdan sonra balansınıza əlavə olunacaq.",
                    Url = $"/Order/Track/{orderId}"
                });
            }

            if (order != null)
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = order.UserId,
                    Title = "Kuryer təyin olundu",
                    Message = $"#{orderId} nömrəli sifarişiniz kuryerə tapşırıldı və yola çıxdı.",
                    Url = $"/Order/Track/{orderId}"
                });
            }

            await _context.SaveChangesAsync();
        }

        private static string OrderGroupName(int orderId) => $"order-{orderId}";
    }
}
