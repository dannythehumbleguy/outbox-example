using FluentMigrator.Runner.VersionTableInfo;

namespace OrderService.Infrastructure.Data;

public class VersionTableMetaData : IVersionTableMetaData
{
    public string SchemaName => "orders";
    public string TableName => "version_info";
    public string ColumnName => "version";
    public string UniqueIndexName => "uc_orders_version_info_version";
    public string AppliedOnColumnName => "applied_on";
    public string DescriptionColumnName => "description";
    public object ApplicationContext { get; set; } = null!;
    public bool OwnsSchema => true;
    public bool CreateWithPrimaryKey => false;
}
