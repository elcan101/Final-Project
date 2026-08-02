using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace E_Commerce.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderPostDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rayonlara poçtla çatdırılma: xəritədən seçmədən, rayon və poçt indeksi
            // ilə sifariş vermək üçün lazım olan sütunlar.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'District' AND Object_ID = Object_ID(N'Orders'))
BEGIN
    ALTER TABLE Orders ADD District nvarchar(max) NULL;
END
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'PostalCode' AND Object_ID = Object_ID(N'Orders'))
BEGIN
    ALTER TABLE Orders ADD PostalCode nvarchar(max) NULL;
END
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'IsPostDelivery' AND Object_ID = Object_ID(N'Orders'))
BEGIN
    ALTER TABLE Orders ADD IsPostDelivery bit NOT NULL CONSTRAINT DF_Orders_IsPostDelivery DEFAULT (0);
END
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'TrackingCode' AND Object_ID = Object_ID(N'Orders'))
BEGIN
    ALTER TABLE Orders ADD TrackingCode nvarchar(max) NULL;
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'TrackingCode' AND Object_ID = Object_ID(N'Orders'))
BEGIN
    ALTER TABLE Orders DROP COLUMN TrackingCode;
END
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'IsPostDelivery' AND Object_ID = Object_ID(N'Orders'))
BEGIN
    ALTER TABLE Orders DROP CONSTRAINT DF_Orders_IsPostDelivery;
    ALTER TABLE Orders DROP COLUMN IsPostDelivery;
END
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'PostalCode' AND Object_ID = Object_ID(N'Orders'))
BEGIN
    ALTER TABLE Orders DROP COLUMN PostalCode;
END
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'District' AND Object_ID = Object_ID(N'Orders'))
BEGIN
    ALTER TABLE Orders DROP COLUMN District;
END
");
        }
    }
}
