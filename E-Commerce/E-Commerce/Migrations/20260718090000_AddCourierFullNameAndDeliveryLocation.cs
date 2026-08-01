using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace E_Commerce.Migrations
{
    /// <inheritdoc />
    public partial class AddCourierFullNameAndDeliveryLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // CourierProfile.FullName modeldə var idi, amma heç bir migration-a düşməmişdi.
            // Bu sütunlar bəzi bazalarda artıq əlavə olunmuş ola bilər (əvvəlki uğursuz/yarımçıq
            // "database update" cəhdindən), ona görə hər sütunu əlavə etməzdən əvvəl mövcudluğunu
            // yoxlayırıq — beləliklə migrasiya neçə dəfə işlədilsə də xəta vermir.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'FullName' AND Object_ID = Object_ID(N'CourierProfiles'))
BEGIN
    ALTER TABLE CourierProfiles ADD FullName nvarchar(100) NOT NULL DEFAULT N'Kuryer';
END
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'DeliveryAddressText' AND Object_ID = Object_ID(N'Orders'))
BEGIN
    ALTER TABLE Orders ADD DeliveryAddressText nvarchar(max) NULL;
END
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'DeliveryLatitude' AND Object_ID = Object_ID(N'Orders'))
BEGIN
    ALTER TABLE Orders ADD DeliveryLatitude float NULL;
END
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'DeliveryLongitude' AND Object_ID = Object_ID(N'Orders'))
BEGIN
    ALTER TABLE Orders ADD DeliveryLongitude float NULL;
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'FullName' AND Object_ID = Object_ID(N'CourierProfiles'))
BEGIN
    ALTER TABLE CourierProfiles DROP COLUMN FullName;
END
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'DeliveryAddressText' AND Object_ID = Object_ID(N'Orders'))
BEGIN
    ALTER TABLE Orders DROP COLUMN DeliveryAddressText;
END
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'DeliveryLatitude' AND Object_ID = Object_ID(N'Orders'))
BEGIN
    ALTER TABLE Orders DROP COLUMN DeliveryLatitude;
END
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'DeliveryLongitude' AND Object_ID = Object_ID(N'Orders'))
BEGIN
    ALTER TABLE Orders DROP COLUMN DeliveryLongitude;
END
");
        }
    }
}

