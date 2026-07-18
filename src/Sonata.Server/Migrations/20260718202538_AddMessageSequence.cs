using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sonata.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageSequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "sequence",
                table: "messages",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("""
                                 WITH ranked AS (
                                     SELECT
                                         id,
                                         ROW_NUMBER() OVER (
                                             PARTITION BY session_id
                                             ORDER BY created_at, id
                                         )::integer AS calculated_sequence
                                     FROM messages
                                 )
                                 UPDATE messages
                                 SET sequence = ranked.calculated_sequence
                                 FROM ranked
                                 WHERE messages.id = ranked.id;
                                 """);

            migrationBuilder.AlterColumn<int>(
                name: "sequence",
                table: "messages",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_messages_sequence_positive",
                table: "messages",
                sql: "sequence > 0");

            migrationBuilder.CreateIndex(
                name: "IX_messages_session_id_sequence",
                table: "messages",
                columns: new[] { "session_id", "sequence" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_messages_sessions_session_id",
                table: "messages",
                column: "session_id",
                principalTable: "sessions",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_messages_sessions_session_id",
                table: "messages");

            migrationBuilder.DropIndex(
                name: "IX_messages_session_id_sequence",
                table: "messages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_messages_sequence_positive",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "sequence",
                table: "messages");
        }
    }
}
