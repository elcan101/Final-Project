using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace E_Commerce.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingCashback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'PendingCashback' AND Object_ID = Object_ID(N'Wallets'))
BEGIN
    ALTER TABLE Wallets ADD PendingCashback decimal(18,2) NOT NULL DEFAULT 0;
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'PendingCashback' AND Object_ID = Object_ID(N'Wallets'))
BEGIN
    ALTER TABLE Wallets DROP COLUMN PendingCashback;
END
");
        }
    }
}
