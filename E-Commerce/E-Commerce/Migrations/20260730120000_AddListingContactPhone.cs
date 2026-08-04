using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace E_Commerce.Migrations
{
    public partial class AddListingContactPhone : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'ContactPhone' AND Object_ID = Object_ID(N'Listings'))
BEGIN
    ALTER TABLE Listings ADD ContactPhone nvarchar(30) NULL;
END
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'ContactPhone' AND Object_ID = Object_ID(N'Listings'))
BEGIN
    ALTER TABLE Listings DROP COLUMN ContactPhone;
END
");
        }
    }
}
