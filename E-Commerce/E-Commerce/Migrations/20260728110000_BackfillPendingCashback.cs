using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace E_Commerce.Migrations
{
    /// <inheritdoc />
    public partial class BackfillPendingCashback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // PendingCashback sütunu sonradan əlavə olunub (bax 20260727120000_AddPendingCashback).
            // Bu sütun yaradılmazdan əvvəl qazanılmış keşbek yalnız TotalCashbackEarned-də idi və
            // heç vaxt PendingCashback-ə köçürülməmişdi — nəticədə köhnə istifadəçilər saytda
            // keşbeki balansa köçürə bilmirdi (düymə görünmürdü, çünki PendingCashback 0 idi).
            // Bu skript hələ heç bir köçürmə etməmiş (PendingCashback = 0) və keşbek qazanmış
            // bütün istifadəçilər üçün bir dəfəlik "gözləyən keşbek"i bərpa edir.
            migrationBuilder.Sql(@"
UPDATE Wallets
SET PendingCashback = TotalCashbackEarned
WHERE PendingCashback = 0 AND TotalCashbackEarned > 0;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data-only bərpa migration-udur — geri qaytarmaq təhlükəlidir (istifadəçi artıq
            // köçürmə etmiş ola bilər), ona görə Down heç nə etmir.
        }
    }
}
