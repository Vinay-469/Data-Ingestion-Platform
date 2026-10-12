namespace CoreModels.Entities;

public sealed class Upload
{
    public long UploadId { get; set; }
    public int UserId { get; set; }
    public AppUser? User { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime UploadedOn { get; set; }
    public ICollection<UploadTransaction> Transactions { get; set; } = [];
    public ICollection<IngestionSheet> Sheets { get; set; } = [];
}

public sealed class UploadTransaction
{
    public long TransactionId { get; set; }
    public long UploadId { get; set; }
    public DateTime StartedAt { get; set; }
    public string Status { get; set; } = "Started";
    public int TotalSheets { get; set; }
    public long TotalRows { get; set; }
    public int TotalColumns { get; set; }
    public long DuplicateRows { get; set; }
    public long NullRows { get; set; }
    public long SkippedRows { get; set; }
    public string? ErrorMessage { get; set; }
    public Upload? Upload { get; set; }
}

public sealed class IngestionSheet
{
    public long SheetId { get; set; }
    public long UploadId { get; set; }
    public string SheetName { get; set; } = string.Empty;
    public int SheetOrder { get; set; }
    public long TotalRows { get; set; }
    public int TotalColumns { get; set; }
    public Upload? Upload { get; set; }
    public ICollection<IngestionColumn> Columns { get; set; } = [];
    public ICollection<IngestionRow> Rows { get; set; } = [];
}

public sealed class IngestionColumn
{
    public long ColumnId { get; set; }
    public long SheetId { get; set; }
    public int ColumnOrder { get; set; }
    public string DetectedDataType { get; set; } = "string";
    public bool AllowNull { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public long UniqueValues { get; set; }
    public IngestionSheet? Sheet { get; set; }
    public ICollection<SourceColumn> SourceColumns { get; set; } = [];
}

public sealed class SourceColumn
{
    public long SourceColumnId { get; set; }
    public long ColumnId { get; set; }
    public string SourceColumnName { get; set; } = string.Empty;
    public bool IsMappedToStandard { get; set; }
    public string? MappedStandardColumn { get; set; }
    public bool IsSelectedMetadata { get; set; }
    public string? MappingMethod { get; set; }
    public bool ConfirmedByCustomer { get; set; }
    public DateTime CreatedOn { get; set; }
    public IngestionColumn? Column { get; set; }
    public ICollection<RowMetadataValue> MetadataValues { get; set; } = [];
}

public sealed class RowMetadataValue
{
    public long RowMetadataValueId { get; set; }
    public long RowId { get; set; }
    public long SourceColumnId { get; set; }
    public string? Value { get; set; }
    public string DetectedDataType { get; set; } = "string";
    public string ValidationStatus { get; set; } = "Valid";
    public IngestionRow? Row { get; set; }
    public SourceColumn? SourceColumn { get; set; }
}

public sealed class IngestionRow
{
    public long RowId { get; set; }
    public long SheetId { get; set; }
    public long RowNumber { get; set; }
    public bool HasDuplicate { get; set; }
    public bool HasNull { get; set; }
    public bool IsValid { get; set; } = true;
    public DateTime CreatedOn { get; set; }
    public string? Facility { get; set; }
    public string? Manufacture { get; set; }
    public string? Product { get; set; }
    public string? ProductDescription { get; set; }
    public string? CustomerUom { get; set; }
    public decimal? CustomerQty { get; set; }
    public IngestionSheet? Sheet { get; set; }
    public ICollection<RowMetadataValue> MetadataValues { get; set; } = [];
}

public sealed class DuplicateAudit
{
    public long AuditId { get; set; }
    public long UploadId { get; set; }
    public long SheetId { get; set; }
    public long RowId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string ActionTaken { get; set; } = string.Empty;
    public DateTime CreatedOn { get; set; }
}

public sealed class ValidationAudit
{
    public long ValidationId { get; set; }
    public long UploadId { get; set; }
    public long SheetId { get; set; }
    public long RowId { get; set; }
    public long? ColumnId { get; set; }
    public string ValidationType { get; set; } = string.Empty;
    public string ValidationMessage { get; set; } = string.Empty;
    public DateTime CreatedOn { get; set; }
}
