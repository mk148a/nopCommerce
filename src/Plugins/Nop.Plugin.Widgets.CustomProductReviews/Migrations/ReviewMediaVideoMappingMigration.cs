using FluentMigrator;
using Nop.Data;
using Nop.Data.Mapping;
using Nop.Data.Migrations;
using Nop.Plugin.Widgets.CustomProductReviews.Domains;

namespace Nop.Plugin.Widgets.CustomProductReviews.Migrations;

/// <summary>
/// Older installations of the plugin may have a review-media table created
/// before VideoId was introduced. Make the real video mapping readable rather
/// than suppressing it in the query layer.
/// </summary>
[NopMigration("2026-08-21 12:00:00", "Widgets.CustomProductReviews 1.13. Add review video mapping", MigrationProcessType.Update)]
public sealed class ReviewMediaVideoMappingMigration : MigrationBase
{
    public override void Up()
    {
        if (!DataSettingsManager.IsDatabaseInstalled())
            return;

        var tableName = NameCompatibilityManager.GetTableName(typeof(CustomProductReviewMapping));
        if (!Schema.Table(tableName).Column(nameof(CustomProductReviewMapping.VideoId)).Exists())
        {
            Alter.Table(tableName)
                .AddColumn(nameof(CustomProductReviewMapping.VideoId))
                .AsInt32()
                .Nullable();
        }
    }

    public override void Down()
    {
        // Rollback intentionally keeps user-uploaded media mappings intact.
    }
}
