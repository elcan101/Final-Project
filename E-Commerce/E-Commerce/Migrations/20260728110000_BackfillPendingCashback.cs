using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace E_Commerce.Migrations
{
    public partial class BackfillPendingCashback : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
           
            migrationBuilder.Sql(@"
UPDATE Wallets
SET PendingCashback = TotalCashbackEarned
WHERE PendingCashback = 0 AND TotalCashbackEarned > 0;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            
        }
    }
}
