using Microsoft.EntityFrameworkCore.Migrations;

namespace FindSimilarServices.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Track",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Artist = table.Column<string>(nullable: true),
                    Title = table.Column<string>(nullable: true),
                    ISRC = table.Column<string>(nullable: true),
                    Album = table.Column<string>(nullable: true),
                    ReleaseYear = table.Column<int>(nullable: false),
                    TrackLengthSec = table.Column<double>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Track", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubFingerprint",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TrackId = table.Column<int>(nullable: false),
                    SequenceNumber = table.Column<int>(nullable: false),
                    SequenceAt = table.Column<double>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubFingerprint", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubFingerprint_Track_TrackId",
                        column: x => x.TrackId,
                        principalTable: "Track",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Hash",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HashTable = table.Column<int>(nullable: false),
                    HashBin = table.Column<int>(nullable: false),
                    TrackId = table.Column<int>(nullable: false),
                    SubFingerprintId = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hash", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Hash_SubFingerprint_SubFingerprintId",
                        column: x => x.SubFingerprintId,
                        principalTable: "SubFingerprint",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Hash_Track_TrackId",
                        column: x => x.TrackId,
                        principalTable: "Track",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Hash_SubFingerprintId",
                table: "Hash",
                column: "SubFingerprintId");

            migrationBuilder.CreateIndex(
                name: "IX_Hash_TrackId",
                table: "Hash",
                column: "TrackId");

            migrationBuilder.CreateIndex(
                name: "IX_SubFingerprint_TrackId",
                table: "SubFingerprint",
                column: "TrackId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Hash");

            migrationBuilder.DropTable(
                name: "SubFingerprint");

            migrationBuilder.DropTable(
                name: "Track");
        }
    }
}
