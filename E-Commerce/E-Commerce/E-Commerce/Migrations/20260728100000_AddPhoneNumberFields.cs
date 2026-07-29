using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace E_Commerce.Migrations
{
    /// <inheritdoc />
    public partial class AddPhoneNumberFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Kuryer qeydiyyatı zamanı əlaqə nömrəsi tələb olunur
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'PhoneNumber' AND Object_ID = Object_ID(N'CourierProfiles'))
BEGIN
    ALTER TABLE CourierProfiles ADD PhoneNumber nvarchar(30) NOT NULL DEFAULT '';
END
");

            // Sifariş verərkən müştərinin əlaqə nömrəsi tələb olunur (kuryerin müştəri ilə əlaqə saxlaması üçün)
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'PhoneNumber' AND Object_ID = Object_ID(N'Orders'))
BEGIN
    ALTER TABLE Orders ADD PhoneNumber nvarchar(30) NULL;
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'PhoneNumber' AND Object_ID = Object_ID(N'Orders'))
BEGIN
    ALTER TABLE Orders DROP COLUMN PhoneNumber;
END
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'PhoneNumber' AND Object_ID = Object_ID(N'CourierProfiles'))
BEGIN
    ALTER TABLE CourierProfiles DROP COLUMN PhoneNumber;
END
");
        }
    }
}
