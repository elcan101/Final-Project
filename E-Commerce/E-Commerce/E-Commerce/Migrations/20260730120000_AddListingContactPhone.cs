using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace E_Commerce.Migrations
{
    /// <inheritdoc />
    public partial class AddListingContactPhone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // C2C bazarında elan verərkən satıcının göstərdiyi əlaqə nömrəsi
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'ContactPhone' AND Object_ID = Object_ID(N'Listings'))
BEGIN
    ALTER TABLE Listings ADD ContactPhone nvarchar(30) NULL;
END
");
        }

        /// <inheritdoc />
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
