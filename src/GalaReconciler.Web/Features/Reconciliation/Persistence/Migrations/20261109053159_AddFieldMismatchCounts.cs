using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GalaReconciler.Web.Features.Reconciliation.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFieldMismatchCounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PositionMismatchCount",
                table: "Runs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PostNominalMismatchCount",
                table: "Runs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TitleMismatchCount",
                table: "Runs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PositionMismatchCount",
                table: "Runs");

            migrationBuilder.DropColumn(
                name: "PostNominalMismatchCount",
                table: "Runs");

            migrationBuilder.DropColumn(
                name: "TitleMismatchCount",
                table: "Runs");
        }
    }
}
