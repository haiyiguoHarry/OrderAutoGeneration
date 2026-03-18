using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HelloOrder.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConversionJobMerchantAndOrderConversionJobId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "conversion_job_id",
                table: "order",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "merchant_id",
                table: "conversion_job",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "merchant_shop_id",
                table: "conversion_job",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_order_conversion_job_id",
                table: "order",
                column: "conversion_job_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_order_conversion_job_id",
                table: "order");

            migrationBuilder.DropColumn(
                name: "conversion_job_id",
                table: "order");

            migrationBuilder.DropColumn(
                name: "merchant_id",
                table: "conversion_job");

            migrationBuilder.DropColumn(
                name: "merchant_shop_id",
                table: "conversion_job");
        }
    }
}
