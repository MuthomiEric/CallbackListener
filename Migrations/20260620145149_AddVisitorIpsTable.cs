using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CallbackListener.Migrations
{
    /// <inheritdoc />
    public partial class AddVisitorIpsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VisitorIps",
                columns: table => new
                {
                    IpHash = table.Column<string>(type: "text", nullable: false),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisitorIps", x => x.IpHash);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VisitorIps");
        }
    }
}
