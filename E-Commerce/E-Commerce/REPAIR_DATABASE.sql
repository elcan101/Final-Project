

SET NOCOUNT ON;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'FullName' AND Object_ID = Object_ID(N'CourierProfiles'))
BEGIN
    ALTER TABLE CourierProfiles ADD FullName nvarchar(100) NOT NULL DEFAULT N'Kuryer';
    PRINT 'CourierProfiles.FullName əlavə olundu.';
END
ELSE
    PRINT 'CourierProfiles.FullName artıq mövcuddur, toxunulmadı.';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'DeliveryAddressText' AND Object_ID = Object_ID(N'Orders'))
BEGIN
    ALTER TABLE Orders ADD DeliveryAddressText nvarchar(max) NULL;
    PRINT 'Orders.DeliveryAddressText əlavə olundu.';
END
ELSE
    PRINT 'Orders.DeliveryAddressText artıq mövcuddur, toxunulmadı.';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'DeliveryLatitude' AND Object_ID = Object_ID(N'Orders'))
BEGIN
    ALTER TABLE Orders ADD DeliveryLatitude float NULL;
    PRINT 'Orders.DeliveryLatitude əlavə olundu.';
END
ELSE
    PRINT 'Orders.DeliveryLatitude artıq mövcuddur, toxunulmadı.';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'DeliveryLongitude' AND Object_ID = Object_ID(N'Orders'))
BEGIN
    ALTER TABLE Orders ADD DeliveryLongitude float NULL;
    PRINT 'Orders.DeliveryLongitude əlavə olundu.';
END
ELSE
    PRINT 'Orders.DeliveryLongitude artıq mövcuddur, toxunulmadı.';

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
