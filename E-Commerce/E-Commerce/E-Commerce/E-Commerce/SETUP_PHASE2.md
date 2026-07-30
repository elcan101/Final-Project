# Okean Kitabevi — Əlavə Edilmiş Modullar (2-ci mərhələ)

Bu mərhələdə əlavə olunanlar: **Abunəlik + İcarə sistemi**, **SignalR kuryer izləmə + canlı çat**,
**C2C Bazar + Kupon**, **Turbo.az tipli AJAX filtrasiya**, **Daimi səbət (kupon dəstəyi ilə)** və
tam **footer (əlaqə/sosial şəbəkə)**.

## ⚠️ İşə salmadan əvvəl

Bu kodu mən compile/test edə bilmədim — mühitimdə .NET SDK yoxdur. Sənin tərəfdə:

```bash
cd E-Commerce/E-Commerce
dotnet restore
dotnet ef migrations add AddCommerceFeatures
dotnet ef database update
dotnet run
```

Yeni migrasiya lazımdır, çünki bu cədvəllər/sütunlar əlavə olunub:
`UserSubscriptions`, `BookRentals`, `Listings`, `ChatMessages`, `PaymentMethods`,
`Products.Author`, `Orders.CouponCode/DiscountAmount/CourierLatitude/CourierLongitude/LastLocationUpdate`.

## Nə əlavə olundu

### 1) Abunəlik + İcarə (`SubscriptionController`, `RentalController`)
- Standart (2.99 AZN/ay) və Premium (9.99 AZN/ay) planları, spesifikasiyadakı xüsusiyyət cədvəli ilə.
- İcarə: gündə 0.20 AZN, Premium-da ayda 1 pulsuz icarə (14 gün), gecikmədə gündə 0.40 AZN cərimə.
- Ödəniş: əvvəlcə balansdan, çatmayan hissə (mock) kartdan.

### 2) SignalR (`Hubs/CourierTrackingHub.cs`, `Hubs/ChatHub.cs`)
- `OrderController.MarkReady()` sifarişi "Hazırdır" edir → bütün boşda kuryerlərə broadcast gedir.
- `AcceptOrder` DB-də şərtli `ExecuteUpdateAsync` ilə **ilk təsdiqləyən qazanır**, yarış vəziyyəti yoxdur.
- `Order/Track` səhifəsində canlı GPS (demo simulyasiya) + canlı çat.
- Kuryer paneli: `/Courier/Dashboard`.

### 3) Mock Stripe (`Services/MockStripePaymentService.cs`)
- Kart nömrəsi HEÇ VAXT saxlanılmır, yalnız saxta token. `Services/IPaymentService.cs`-də production
  üçün Stripe.NET-ə keçid təlimatı yazılıb (şərh şəklində).

### 4) Gündəlik hesablaşma (`Services/DailyBillingService.cs`)
- `IHostedService`: aktiv C2C elanlarından gündəlik 0.10 AZN, gecikmiş icarələrdən cərimə avtomatik
  balansdan tutulur.

### 5) C2C Bazar (`ListingController`)
- Elan Ver → Alıcı Tap → Qazanc Əldə Et. Satışdan 8% komissiya, qalanı satıcının balansına keçir.

### 6) Kupon + Loyallıq
- `CartController.ApplyCoupon` sessiyada saxlayır, `OrderController.Checkout`-da tətbiq olunur.
- Cashback artıq **faktiki olaraq** `Wallet.Balance`-a yazılır (əvvəlki versiyada yalnız `Order` sahəsində qalırdı).

### 7) Turbo.az filtrasiyası (`ProductController.Index/FilterAjax`, `wwwroot/js/product-filter.js`)
- Kateqoriya, müəllif, qiymət aralığı, sıralama — AJAX ilə, sayfa yenilənmədən.

### 8) Footer
- WhatsApp (+994 51 487 01 46), Instagram/TikTok (@okeankitabevi), "2021-dən bu yana istifadə edilir".

## Test etmək üçün

- İki fərqli brauzer/inkoqnito ilə: birində müştəri kimi sifariş ver, "Hazırdır elan et" bas;
  digərində `/Courier/Dashboard`-da kuryer kimi online ol — sifariş siqnalını görəcəksən.
- `/Order/Track/{id}` səhifəsində canlı çat və simulyasiya edilmiş kuryer mövqeyini test et.

## ⚠️ 3-cü mərhələ (bu redaktədə əlavə olundu) — yeni migrasiya lazımdır

Bu dəfə də kodu compile/test edə bilmədim (mühitimdə hələ də .NET SDK/şəbəkə yoxdur). İşə salmadan əvvəl:

```bash
cd E-Commerce/E-Commerce
dotnet restore
dotnet ef migrations add AddProductDetailsAndReviews
dotnet ef database update
dotnet run
```

Yeni sütun/cədvəllər: `Products.Publisher`, `Products.Language`, `Products.PageCount`,
və yeni `ProductReviews` cədvəli (ProductId, UserId, UserName, Rating, Comment).

### Bu mərhələdə edilənlər
- **Virtual 3D Kitab Rəfi tamamilə silindi** (`_Bookshelf3D.cshtml`, `bookshelf3d.js`, əlaqəli CSS və Home səhifəsindəki çağırış).
- Footer arxa fonu mavi edildi, "2021-dən bu yana" yazısı "2021 - dən etibarən xidmətinizdəyik" ilə əvəzləndi.
- `CategoryController.Create` (GET/POST) yalnız Admin roluna bağlandı — əvvəllər hər kəs kateqoriya əlavə edə bilirdi.
- Kateqoriya kartlarının klik/keçid məntiqi sərtləşdirildi (`asp-controller` açıq yazıldı, `Products` action-u siyahı səhifəsi üçün lazım olan `ViewBag`-ləri doldurur).
- Kitab detalları səhifəsinə: müəllif/nəşriyyat göstərilməsi, "Xüsusiyyətlər" düyməsi (dil, səhifə sayı, cild, kateqoriya, qısa məzmun) və müştəri şərh/reytinq bölməsi əlavə olundu (`ProductController.AddReview`).
