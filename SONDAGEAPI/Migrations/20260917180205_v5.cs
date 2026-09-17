using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SONDAGEAPI.Migrations
{
    /// <inheritdoc />
    public partial class v5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SurveySubmissions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    SurveyId = table.Column<string>(type: "TEXT", nullable: false),
                    SubmitterId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurveySubmissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Answer",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    QuestionId = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<int>(type: "INTEGER", nullable: false),
                    SurveySubmissionId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Answer", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Answer_SurveySubmissions_SurveySubmissionId",
                        column: x => x.SurveySubmissionId,
                        principalTable: "SurveySubmissions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Answer_SurveySubmissionId",
                table: "Answer",
                column: "SurveySubmissionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Answer");

            migrationBuilder.DropTable(
                name: "SurveySubmissions");
        }
    }
}
