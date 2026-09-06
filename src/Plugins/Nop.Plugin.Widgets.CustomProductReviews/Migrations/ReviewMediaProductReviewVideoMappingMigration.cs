using FluentMigrator;
using Nop.Data;
using Nop.Data.Mapping;
using Nop.Data.Migrations;
using Nop.Plugin.Widgets.CustomProductReviews.Domains;

namespace Nop.Plugin.Widgets.CustomProductReviews.Migrations;

/// <summary>Bridges the current review-video store without rewriting legacy mapping data.</summary>
[NopMigration("2026-08-21 16:45:00", "Widgets.CustomProductReviews 1.14. Add ProductReviewVideo mapping", MigrationProcessType.Update)]
public sealed class ReviewMediaProductReviewVideoMappingMigration : MigrationBase
{
    public override void Up()
    {
        if (!DataSettingsManager.IsDatabaseInstalled())
            return;

        var tableName = NameCompatibilityManager.GetTableName(typeof(CustomProductReviewMapping));
        if (!Schema.Table(tableName).Column(nameof(CustomProductReviewMapping.ProductReviewVideoId)).Exists())
        {
            Alter.Table(tableName)
                .AddColumn(nameof(CustomProductReviewMapping.ProductReviewVideoId))
                .AsInt32()
                .Nullable();
        }
    }

    public override void Down()
    {
        // Uploaded review media must remain recoverable on rollback.
    }
}
