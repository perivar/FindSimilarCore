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
                    SequenceAt = table.Column<float>(nullable: false),
                    HashTable0 = table.Column<int>(nullable: false),
                    HashTable1 = table.Column<int>(nullable: false),
                    HashTable2 = table.Column<int>(nullable: false),
                    HashTable3 = table.Column<int>(nullable: false),
                    HashTable4 = table.Column<int>(nullable: false),
                    HashTable5 = table.Column<int>(nullable: false),
                    HashTable6 = table.Column<int>(nullable: false),
                    HashTable7 = table.Column<int>(nullable: false),
                    HashTable8 = table.Column<int>(nullable: false),
                    HashTable9 = table.Column<int>(nullable: false),
                    HashTable10 = table.Column<int>(nullable: false),
                    HashTable11 = table.Column<int>(nullable: false),
                    HashTable12 = table.Column<int>(nullable: false),
                    HashTable13 = table.Column<int>(nullable: false),
                    HashTable14 = table.Column<int>(nullable: false),
                    HashTable15 = table.Column<int>(nullable: false),
                    HashTable16 = table.Column<int>(nullable: false),
                    HashTable17 = table.Column<int>(nullable: false),
                    HashTable18 = table.Column<int>(nullable: false),
                    HashTable19 = table.Column<int>(nullable: false),
                    HashTable20 = table.Column<int>(nullable: false),
                    HashTable21 = table.Column<int>(nullable: false),
                    HashTable22 = table.Column<int>(nullable: false),
                    HashTable23 = table.Column<int>(nullable: false),
                    HashTable24 = table.Column<int>(nullable: false),
                    Clusters = table.Column<string>(nullable: true)
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

            migrationBuilder.CreateIndex(
                name: "IX_SubFingerprint_TrackId_HashTable0_HashTable1_HashTable2_HashTable3_HashTable4_HashTable5_HashTable6_HashTable7_HashTable8_HashTable9_HashTable10_HashTable11_HashTable12_HashTable13_HashTable14_HashTable15_HashTable16_HashTable17_HashTable18_HashTable19_HashTable20_HashTable21_HashTable22_HashTable23_HashTable24",
                table: "SubFingerprint",
                columns: new[] { "TrackId", "HashTable0", "HashTable1", "HashTable2", "HashTable3", "HashTable4", "HashTable5", "HashTable6", "HashTable7", "HashTable8", "HashTable9", "HashTable10", "HashTable11", "HashTable12", "HashTable13", "HashTable14", "HashTable15", "HashTable16", "HashTable17", "HashTable18", "HashTable19", "HashTable20", "HashTable21", "HashTable22", "HashTable23", "HashTable24" });

            migrationBuilder.CreateIndex(
                name: "IX_Track_Title",
                table: "Track",
                column: "Title");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubFingerprint");

            migrationBuilder.DropTable(
                name: "Track");
        }
    }
}
