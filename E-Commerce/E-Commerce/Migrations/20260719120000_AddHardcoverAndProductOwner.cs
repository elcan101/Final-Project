using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace E_Commerce.Migrations
{
    public partial class AddHardcoverAndProductOwner : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
           
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'IsHardcover' AND Object_ID = Object_ID(N'Products'))
BEGIN
    ALTER TABLE Products ADD IsHardcover bit NOT NULL DEFAULT 1;
END
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'AddedByUserId' AND Object_ID = Object_ID(N'Products'))
BEGIN
    ALTER TABLE Products ADD AddedByUserId nvarchar(450) NULL;
END
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'IsHardcover' AND Object_ID = Object_ID(N'Listings'))
BEGIN
    ALTER TABLE Listings ADD IsHardcover bit NOT NULL DEFAULT 1;
END
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'IsHardcover' AND Object_ID = Object_ID(N'Products'))
BEGIN
    ALTER TABLE Products DROP COLUMN IsHardcover;
END
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'AddedByUserId' AND Object_ID = Object_ID(N'Products'))
BEGIN
    ALTER TABLE Products DROP COLUMN AddedByUserId;
END
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'IsHardcover' AND Object_ID = Object_ID(N'Listings'))
BEGIN
    ALTER TABLE Listings DROP COLUMN IsHardcover;
END
");
        }
    }
}
