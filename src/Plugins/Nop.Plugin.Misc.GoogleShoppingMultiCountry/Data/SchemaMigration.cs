using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using FluentMigrator;
using Microsoft.Data.SqlClient;
using Nop.Core;
using Nop.Data;
using Nop.Data.Extensions;
using Nop.Data.Mapping;
using Nop.Data.Migrations;
using Nop.Plugin.Misc.GoogleShoppingMultiCountry.Domains;

namespace Nop.Plugin.Misc.GoogleShoppingMultiCountry.Migrations
{
    [NopMigration("2024/01/30 09:40:55:1687541", "Nop.Plugin.Misc.GoogleShoppingMultiCountry base schema", MigrationProcessType.Installation)]
    public class SchemaMigration : FluentMigrator.Migration
    {
        #region Fields

        private static readonly ConcurrentDictionary<string, Dictionary<string, ActualColumn>> _columnCache = new();
        private readonly INopDataProvider _dataProvider;
        const string databaseType = "sqlserver";

        #endregion

        #region Ctor

        public SchemaMigration(INopDataProvider dataProvider)
        {
            _dataProvider = dataProvider;
        }

        #endregion

        #region Utilities

        private class ExpectedColumn
        {
            public string ColumnName { get; set; }
            public string SqlDataType { get; set; }
            public bool IsNullable { get; set; }
            public object DefaultValue { get; set; }
        }

        private class ActualColumn
        {
            public string ColumnName { get; set; }
            public string SqlDataType { get; set; }
            public bool IsNullable { get; set; }
        }

        #endregion

        #region Methods

        public override void Up()
        {
            if (!Schema.Table(nameof(GoogleFeedProductRecord)).Exists())
            {
                Create.TableFor<GoogleFeedProductRecord>();
            }

            if (!Schema.Table(nameof(GoogleTaxonomyRecord)).Exists())
            {
                Create.TableFor<GoogleTaxonomyRecord>();
            }

            if (!Schema.Table(nameof(CategoryGoogleTaxonomyRecordMapping)).Exists())
            {
                Create.TableFor<CategoryGoogleTaxonomyRecordMapping>();
            }
        }

        public override void Down()
        {
            if (Schema.Table(nameof(CategoryGoogleTaxonomyRecordMapping)).Exists())
            {
                Delete.Table(nameof(CategoryGoogleTaxonomyRecordMapping));
            }

            if (Schema.Table(nameof(GoogleTaxonomyRecord)).Exists())
            {
                Delete.Table(nameof(GoogleTaxonomyRecord));
            }

            if (Schema.Table(nameof(GoogleFeedProductRecord)).Exists())
            {
                Delete.Table(nameof(GoogleFeedProductRecord));
            }
        }

        #endregion

        #region Helpers

        private void CreateOrAlterTable<TEntity>() where TEntity : BaseEntity
        {
            var tableName = GetTableName<TEntity>();

            if (!Schema.Table(tableName).Exists())
            {
                Create.TableFor<TEntity>();
                return;
            }

            var expectedColumns = GetExpectedColumns(typeof(TEntity));
            var actualColumns = GetActualColumns(tableName);

            foreach (var expected in expectedColumns)
            {
                if (!actualColumns.TryGetValue(expected.ColumnName, out var actual))
                {
                    // Yeni kolon ekle
                    var createColumn = Alter.Table(tableName)
                        .AddColumn(expected.ColumnName)
                        .AsCustom(expected.SqlDataType);

                    if (expected.IsNullable)
                        createColumn.Nullable();
                    else
                        createColumn.NotNullable();

                    if (expected.DefaultValue != null)
                        createColumn.WithDefaultValue(expected.DefaultValue);
                }
                else
                {
                    // Kolon özelliklerini kontrol et
                    var needsAlter = !SqlTypeEquals(expected.SqlDataType, actual.SqlDataType) ||
                                    expected.IsNullable != actual.IsNullable;

                    if (needsAlter)
                    {
                        var alterColumnExpression = Alter.Column(expected.ColumnName)
                            .OnTable(tableName)
                            .AsCustom(expected.SqlDataType);

                        if (expected.IsNullable)
                            alterColumnExpression.Nullable();
                        else
                            alterColumnExpression.NotNullable();
                    }
                }
            }
        }

        private bool SqlTypeEquals(string type1, string type2)
        {
            return string.Equals(
                type1?.Replace(" ", string.Empty),
                type2?.Replace(" ", string.Empty),
                StringComparison.OrdinalIgnoreCase);
        }

        private async void DeleteTableIfEmpty<TEntity>() where TEntity : BaseEntity
        {
            var tableName = GetTableName<TEntity>();

            if (!Schema.Table(tableName).Exists())
                return;

            var rowCount = await _dataProvider.QueryAsync<int>($"SELECT COUNT(*) FROM {tableName}");
            if (rowCount.Count == 0)
                Delete.Table(tableName);
        }

        private string GetTableName<TEntity>()
        {
            return NameCompatibilityManager.GetTableName(typeof(TEntity));
        }

        private List<ExpectedColumn> GetExpectedColumns(Type modelType)
        {
            return modelType.GetProperties()
                .Where(p => !p.GetCustomAttributes<NotMappedAttribute>().Any())
                .Select(prop =>
                {
                    var columnAttribute = prop.GetCustomAttribute<ColumnAttribute>();
                    var defaultValueAttribute = prop.GetCustomAttribute<DefaultValueAttribute>();

                    return new ExpectedColumn
                    {
                        ColumnName = columnAttribute?.Name ?? prop.Name,
                        SqlDataType = GetSqlType(prop.PropertyType, columnAttribute),
                        IsNullable = IsNullableType(prop.PropertyType),
                        DefaultValue = defaultValueAttribute?.Value
                    };
                }).ToList();
        }

        private Dictionary<string, ActualColumn> GetActualColumns(string tableName)
        {
            return _columnCache.GetOrAdd(tableName, tn =>
            {
                var columns = new Dictionary<string, ActualColumn>(StringComparer.OrdinalIgnoreCase);
                var (schema, pureTableName) = ParseSchemaAndTableName(tn);
                var dataSettings = DataSettingsManager.LoadSettings();

                using var connection = new SqlConnection(dataSettings.ConnectionString);
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT 
                        COLUMN_NAME, 
                        DATA_TYPE, 
                        CHARACTER_MAXIMUM_LENGTH,
                        NUMERIC_PRECISION,
                        NUMERIC_SCALE,
                        IS_NULLABLE
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = @Schema AND TABLE_NAME = @TableName";

                command.Parameters.AddWithValue("@Schema", schema);
                command.Parameters.AddWithValue("@TableName", pureTableName);

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var colName = reader["COLUMN_NAME"].ToString();
                    var dataType = reader["DATA_TYPE"].ToString().ToUpper();
                    var maxLength = reader["CHARACTER_MAXIMUM_LENGTH"] as int?;
                    var precision = reader["NUMERIC_PRECISION"] as byte?;
                    var scale = reader["NUMERIC_SCALE"] as int?;
                    var isNullable = reader["IS_NULLABLE"].ToString() == "YES";

                    // Data type formatting
                    var sqlType = dataType switch
                    {
                        "VARCHAR" => maxLength == -1 ? "VARCHAR(MAX)" : $"VARCHAR({maxLength})",
                        "NVARCHAR" => maxLength == -1 ? "NVARCHAR(MAX)" : $"NVARCHAR({maxLength})",
                        "DECIMAL" => $"DECIMAL({precision},{scale})",
                        _ => dataType
                    };

                    columns[colName] = new ActualColumn
                    {
                        ColumnName = colName,
                        SqlDataType = sqlType,
                        IsNullable = isNullable
                    };
                }
                return columns;
            });
        }

        private (string Schema, string TableName) ParseSchemaAndTableName(string fullName)
        {
            var parts = fullName.Split('.');
            return parts.Length switch
            {
                1 => ("dbo", parts[0]),
                2 => (parts[0], parts[1]),
                _ => throw new Exception($"Invalid table name format: {fullName}")
            };
        }

        private string GetSqlType(Type type, ColumnAttribute columnAttribute)
        {
            if (!string.IsNullOrEmpty(columnAttribute?.TypeName))
                return columnAttribute.TypeName;

            type = Nullable.GetUnderlyingType(type) ?? type;

            return type.Name switch
            {
                "Int32" => "INT",
                "String" => "NVARCHAR(MAX)",
                "Boolean" => "BIT",
                "DateTime" => "DATETIME2",
                "Double" => "FLOAT",
                "Decimal" => "DECIMAL(18,4)",
                "Byte[]" => "VARBINARY(MAX)",
                "Guid" => "UNIQUEIDENTIFIER",
                "Int64" => "BIGINT",
                _ => throw new NotSupportedException($"Unsupported type: {type.FullName}")
            };
        }

        private bool IsNullableType(Type type)
        {
            return Nullable.GetUnderlyingType(type) != null ||
                   (type.IsClass && type != typeof(string)) ||
                   type == typeof(string);
        }

        #endregion
    }
}