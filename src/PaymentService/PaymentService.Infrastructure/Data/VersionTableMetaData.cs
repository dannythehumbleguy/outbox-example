using FluentMigrator.Runner.VersionTableInfo;

namespace PaymentService.Infrastructure.Data;

public class VersionTableMetaData : IVersionTableMetaData
{
    public string SchemaName => "payment";
    public string TableName => "version_info";
    public string ColumnName => "version";
    public string UniqueIndexName => "uc_payment_version_info_version";
    public string AppliedOnColumnName => "applied_on";
    public string DescriptionColumnName => "description";
    public object ApplicationContext { get; set; } = null!;
    public bool OwnsSchema => false;
    public bool CreateWithPrimaryKey => false;
}
