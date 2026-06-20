using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CallbackListener.Migrations
{
    /// <inheritdoc />
    public partial class AddCallbacksReceived : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "TotalCallbacksReceived",
                table: "AspNetUsers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TotalCallbacksReceived",
                table: "AspNetUsers");
        }
    }
}
