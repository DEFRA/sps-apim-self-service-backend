using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sps.Apim.SelfServe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAccessRequestDecisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AccountUserId",
                table: "AccessRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignedRole",
                table: "AccessRequests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DecidedAtUtc",
                table: "AccessRequests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DecidedByUserId",
                table: "AccessRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DecisionEmailSentAtUtc",
                table: "AccessRequests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisionNote",
                table: "AccessRequests",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "AccessRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "AccessRequests",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "AccessRequests",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "AwaitingReview");

            migrationBuilder.CreateIndex(
                name: "IX_AccessRequests_Status",
                table: "AccessRequests",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AccessRequests_Status",
                table: "AccessRequests");

            migrationBuilder.DropColumn(
                name: "AccountUserId",
                table: "AccessRequests");

            migrationBuilder.DropColumn(
                name: "AssignedRole",
                table: "AccessRequests");

            migrationBuilder.DropColumn(
                name: "DecidedAtUtc",
                table: "AccessRequests");

            migrationBuilder.DropColumn(
                name: "DecidedByUserId",
                table: "AccessRequests");

            migrationBuilder.DropColumn(
                name: "DecisionEmailSentAtUtc",
                table: "AccessRequests");

            migrationBuilder.DropColumn(
                name: "DecisionNote",
                table: "AccessRequests");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "AccessRequests");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AccessRequests");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "AccessRequests");
        }
    }
}
