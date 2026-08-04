using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace E_Commerce.Migrations
{
    public partial class AddDeliveryFeeAndNotifications : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'DeliveryFee' AND Object_ID = Object_ID(N'Orders'))
BEGIN
    ALTER TABLE Orders ADD DeliveryFee decimal(18,2) NOT NULL DEFAULT 0;
END
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'DeliveryDistanceKm' AND Object_ID = Object_ID(N'Orders'))
BEGIN
    ALTER TABLE Orders ADD DeliveryDistanceKm float NULL;
END
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE Name = N'Notifications')
BEGIN
    CREATE TABLE Notifications (
        Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
        UserId nvarchar(450) NOT NULL,
        Title nvarchar(150) NOT NULL,
        Message nvarchar(max) NOT NULL,
        Url nvarchar(max) NULL,
        IsRead bit NOT NULL DEFAULT 0,
        IsDeleted bit NOT NULL DEFAULT 0,
        CreatedDate datetime2 NOT NULL DEFAULT GETDATE(),
        UpdatedDate datetime2 NULL
    );

    CREATE INDEX IX_Notifications_UserId ON Notifications(UserId);
END
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.tables WHERE Name = N'Notifications')
BEGIN
    DROP TABLE Notifications;
END
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'DeliveryDistanceKm' AND Object_ID = Object_ID(N'Orders'))
BEGIN
    ALTER TABLE Orders DROP COLUMN DeliveryDistanceKm;
END
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'DeliveryFee' AND Object_ID = Object_ID(N'Orders'))
BEGIN
    ALTER TABLE Orders DROP COLUMN DeliveryFee;
END
");
        }
    }
}
