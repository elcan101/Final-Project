using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace E_Commerce.Migrations
{
    /// <inheritdoc />
    public partial class AddRentalWarningAndFineFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'DueSoonEmailSent' AND Object_ID = Object_ID(N'BookRentals'))
BEGIN
    ALTER TABLE BookRentals ADD DueSoonEmailSent bit NOT NULL DEFAULT 0;
END
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'LateFineApplied' AND Object_ID = Object_ID(N'BookRentals'))
BEGIN
    ALTER TABLE BookRentals ADD LateFineApplied bit NOT NULL DEFAULT 0;
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'DueSoonEmailSent' AND Object_ID = Object_ID(N'BookRentals'))
BEGIN
    ALTER TABLE BookRentals DROP COLUMN DueSoonEmailSent;
END
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'LateFineApplied' AND Object_ID = Object_ID(N'BookRentals'))
BEGIN
    ALTER TABLE BookRentals DROP COLUMN LateFineApplied;
END
");
        }
    }
}
