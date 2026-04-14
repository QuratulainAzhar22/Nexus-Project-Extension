using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexus_backend.Migrations
{
    /// <inheritdoc />
    public partial class AddESignatureToDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSigned",
                table: "Documents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SignatureImageUrl",
                table: "Documents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SignedAt",
                table: "Documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignedById",
                table: "Documents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignedByUserId",
                table: "Documents",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_SignedById",
                table: "Documents",
                column: "SignedById");

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_AspNetUsers_SignedById",
                table: "Documents",
                column: "SignedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_AspNetUsers_SignedById",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_SignedById",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "IsSigned",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "SignatureImageUrl",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "SignedAt",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "SignedById",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "SignedByUserId",
                table: "Documents");
        }
    }
}
