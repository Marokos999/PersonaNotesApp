using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonaNotesApp.API.Migrations
{
    /// <inheritdoc />
    public partial class AddNoteRenderVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RenderVersion",
                table: "Notes",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RenderVersion",
                table: "Notes");
        }
    }
}
