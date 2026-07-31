using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace E_Commerce.Migrations
{
    /// <inheritdoc />
    public partial class AddRentalOrderDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // İcarəyə götürülən kitablar da digər sifarişlər kimi çatdırılma ünvanı seçib
            // kuryerlə çatdırılsın deyə, hər icarəni əlaqəli bir Order-ə bağlayırıq
            // (çatdırılma ünvanı, kuryer təyinatı və canlı izləmə Order üzərindən aparılır).
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'OrderId' AND Object_ID = Object_ID(N'BookRentals'))
BEGIN
    ALTER TABLE BookRentals ADD OrderId int NULL;
END
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_BookRentals_OrderId' AND object_id = Object_ID(N'BookRentals'))
BEGIN
    CREATE INDEX IX_BookRentals_OrderId ON BookRentals (OrderId);
END
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_BookRentals_Orders_OrderId')
BEGIN
    ALTER TABLE BookRentals ADD CONSTRAINT FK_BookRentals_Orders_OrderId
        FOREIGN KEY (OrderId) REFERENCES Orders (Id) ON DELETE SET NULL;
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_BookRentals_Orders_OrderId')
BEGIN
    ALTER TABLE BookRentals DROP CONSTRAINT FK_BookRentals_Orders_OrderId;
END
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_BookRentals_OrderId' AND object_id = Object_ID(N'BookRentals'))
BEGIN
    DROP INDEX IX_BookRentals_OrderId ON BookRentals;
END
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'OrderId' AND Object_ID = Object_ID(N'BookRentals'))
BEGIN
    ALTER TABLE BookRentals DROP COLUMN OrderId;
END
");
        }
    }
}
