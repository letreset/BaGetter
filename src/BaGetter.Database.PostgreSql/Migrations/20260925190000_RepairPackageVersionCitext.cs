using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BaGetter.Database.PostgreSql.Migrations
{
    /// <summary>
    /// Some databases that come from BaGet or upstream BaGetter have "FixVersionCaseSensitivity" in their
    /// migration history while "Packages"."Version" is still character varying(64). This converts the column
    /// to citext only when it isn't already. The model snapshot already says citext, so this is SQL only.
    /// See: https://github.com/letreset/BaGetter/issues/24
    /// </summary>
    public partial class RepairPackageVersionCitext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Converting to citext rebuilds the unique (FeedId, Id, Version) index, which fails if versions
            // that only differ by case were stored while the column was case sensitive. Fail with a clear
            // message instead, so they can be cleaned up by hand.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = current_schema()
                          AND table_name = 'Packages'
                          AND column_name = 'Version'
                          AND udt_name <> 'citext')
                    THEN
                        IF EXISTS (
                            SELECT 1 FROM "Packages"
                            GROUP BY "FeedId", lower("Id"), lower("Version")
                            HAVING count(*) > 1)
                        THEN
                            RAISE EXCEPTION 'Cannot convert "Packages"."Version" to citext: some packages have versions that only differ by case. Delete the duplicates and restart BaGetter.';
                        END IF;

                        ALTER TABLE "Packages" ALTER COLUMN "Version" TYPE citext USING "Version"::citext;
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty: "FixVersionCaseSensitivity" owns the citext column type, so there is
            // nothing to revert here.
        }
    }
}
