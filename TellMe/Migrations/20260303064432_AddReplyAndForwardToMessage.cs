using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TellMe.Migrations
{
    /// <inheritdoc />
    public partial class AddReplyAndForwardToMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ForwardedMessageId",
                table: "FacebookMessages",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ReplyToId",
                table: "FacebookMessages",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ForwardedMessageId",
                table: "FacebookMessages");

            migrationBuilder.DropColumn(
                name: "ReplyToId",
                table: "FacebookMessages");
        }
    }
}
