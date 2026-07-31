/*
=====================================================================================
 REPAIR_DATABASE.sql
=====================================================================================
 NƏ ÜÇÜNDÜR:
   "Column names in each table must be unique. Column name 'FullName' in table
   'CourierProfiles' is specified more than once." xətası, adətən bazada FullName
   sütunu artıq mövcud olduğu halda, EF Core-un (Migrations qovluğunda guard
   olmayan köhnə bir migration faylı üzündən, məs. əvvəllər "Add-Migration
   AddCourierFullName" əmri ilə yaradılmış fayl) həmin sütunu YENİDƏN əlavə
   etməyə çalışmasından yaranır.

 NƏ EDİR BU SKRIPT:
   1) CourierProfiles.FullName və Orders.DeliveryAddressText / DeliveryLatitude /
      DeliveryLongitude sütunlarının bazada mövcud olduğuna əmin olur (yoxdursa əlavə edir).
   2) __EFMigrationsHistory cədvəlində bu layihənin bütün migration-larını "tətbiq
      olunmuş" kimi qeyd edir ki, EF Core artıq bunları BİR DƏHA icra etməyə
      çalışmasın (bu da məhz xətanın qarşısını alır).

 NECƏ İŞLƏDİLİR:
   1) SQL Server Management Studio (və ya Azure Data Studio) açın, düzgün bazaya qoşulun.
   2) Bu faylı açıb tam işə salın (Execute / F5).
   3) Sonra Visual Studio-da MÜTLƏQ bunları edin (aşağıdakı README bölümünə bax):
        - Solution-u bağlayın
        - bin/ və obj/ qovluqlarını silin (E-Commerce layihəsinin içində)
        - Solution-u yenidən açıb "Rebuild" edin
      Bu addım vacibdir, çünki köhnə/guard-sız migration DLL-i keşdə qalıb yenidən
      işə düşməsin deyə.
   4) Migrations qovluğunda "AddCourierFullName" adlı (bizim
      20260718090000_AddCourierFullNameAndDeliveryLocation faylından fərqli) başqa
      bir fayl görsəniz, onu (və onun .Designer.cs qoşasını) silin — həmin fayl
      bu problemi yaradan köhnə, guard olmayan migrationdır.
=====================================================================================
*/

SET NOCOUNT ON;

-- 1) CourierProfiles.FullName
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'FullName' AND Object_ID = Object_ID(N'CourierProfiles'))
BEGIN
    ALTER TABLE CourierProfiles ADD FullName nvarchar(100) NOT NULL DEFAULT N'Kuryer';
    PRINT 'CourierProfiles.FullName əlavə olundu.';
END
ELSE
    PRINT 'CourierProfiles.FullName artıq mövcuddur, toxunulmadı.';

-- 2) Orders.DeliveryAddressText
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'DeliveryAddressText' AND Object_ID = Object_ID(N'Orders'))
BEGIN
    ALTER TABLE Orders ADD DeliveryAddressText nvarchar(max) NULL;
    PRINT 'Orders.DeliveryAddressText əlavə olundu.';
END
ELSE
    PRINT 'Orders.DeliveryAddressText artıq mövcuddur, toxunulmadı.';

-- 3) Orders.DeliveryLatitude
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'DeliveryLatitude' AND Object_ID = Object_ID(N'Orders'))
BEGIN
    ALTER TABLE Orders ADD DeliveryLatitude float NULL;
    PRINT 'Orders.DeliveryLatitude əlavə olundu.';
END
ELSE
    PRINT 'Orders.DeliveryLatitude artıq mövcuddur, toxunulmadı.';

-- 4) Orders.DeliveryLongitude
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'DeliveryLongitude' AND Object_ID = Object_ID(N'Orders'))
BEGIN
    ALTER TABLE Orders ADD DeliveryLongitude float NULL;
    PRINT 'Orders.DeliveryLongitude əlavə olundu.';
END
ELSE
    PRINT 'Orders.DeliveryLongitude artıq mövcuddur, toxunulmadı.';

-- 5) __EFMigrationsHistory-ni bu layihənin bütün migration-ları ilə sinxronlaşdır,
--    ki EF Core onları artıq tətbiq olunmuş bilib bir daha icra etməyə çalışmasın.
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260625232231_createtable')
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20260625232231_createtable', N'8.0.27');

IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260716190000_AddIdentityTables')
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20260716190000_AddIdentityTables', N'8.0.27');

IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260717114048_AddCommerceFeatures')
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20260717114048_AddCommerceFeatures', N'8.0.27');

IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260718090000_AddCourierFullNameAndDeliveryLocation')
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20260718090000_AddCourierFullNameAndDeliveryLocation', N'8.0.27');

PRINT '---';
PRINT 'Tamamlandı. İndi Visual Studio-da bin/obj qovluqlarını silib layihəni Rebuild edin.';
