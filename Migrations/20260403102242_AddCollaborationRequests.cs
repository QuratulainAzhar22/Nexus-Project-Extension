using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Nexus_backend.Migrations
{
    /// <inheritdoc />
    public partial class AddCollaborationRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CollaborationRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvestorId = table.Column<string>(type: "text", nullable: false),
                    EntrepreneurId = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollaborationRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollaborationRequests_AspNetUsers_EntrepreneurId",
                        column: x => x.EntrepreneurId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CollaborationRequests_AspNetUsers_InvestorId",
                        column: x => x.InvestorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationRequests_EntrepreneurId",
                table: "CollaborationRequests",
                column: "EntrepreneurId");

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationRequests_InvestorId",
                table: "CollaborationRequests",
                column: "InvestorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CollaborationRequests");
        }
    }
}
