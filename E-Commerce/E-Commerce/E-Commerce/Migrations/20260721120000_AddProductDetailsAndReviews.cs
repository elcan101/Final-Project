using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace E_Commerce.Migrations
{
    /// <inheritdoc />
    public partial class AddProductDetailsAndReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hər sütun/cədvəl əlavə edilmədən əvvəl mövcudluğu yoxlanılır — migrasiya
            // neçə dəfə işlədilsə də xəta vermir (layihənin əvvəlki migrasiyalarındakı üsul).
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'Publisher' AND Object_ID = Object_ID(N'Products'))
BEGIN
    ALTER TABLE Products ADD Publisher nvarchar(150) NULL;
END
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'Language' AND Object_ID = Object_ID(N'Products'))
BEGIN
    ALTER TABLE Products ADD Language nvarchar(50) NULL;
END
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'PageCount' AND Object_ID = Object_ID(N'Products'))
BEGIN
    ALTER TABLE Products ADD PageCount int NULL;
END
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE Name = N'ProductReviews')
BEGIN
    CREATE TABLE ProductReviews (
        Id int NOT NULL IDENTITY(1,1) PRIMARY KEY,
        ProductId int NOT NULL,
        UserId nvarchar(max) NULL,
        UserName nvarchar(100) NOT NULL,
        Rating int NOT NULL,
        Comment nvarchar(1000) NOT NULL,
        IsDeleted bit NOT NULL DEFAULT 0,
        CreatedDate datetime2 NOT NULL,
        UpdatedDate datetime2 NULL,
        CONSTRAINT FK_ProductReviews_Products_ProductId FOREIGN KEY (ProductId) REFERENCES Products(Id) ON DELETE CASCADE
    );

    CREATE INDEX IX_ProductReviews_ProductId ON ProductReviews(ProductId);
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.tables WHERE Name = N'ProductReviews')
BEGIN
    DROP TABLE ProductReviews;
END
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'PageCount' AND Object_ID = Object_ID(N'Products'))
BEGIN
    ALTER TABLE Products DROP COLUMN PageCount;
END
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'Language' AND Object_ID = Object_ID(N'Products'))
BEGIN
    ALTER TABLE Products DROP COLUMN Language;
END
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'Publisher' AND Object_ID = Object_ID(N'Products'))
BEGIN
    ALTER TABLE Products DROP COLUMN Publisher;
END
");
        }
    }
}
