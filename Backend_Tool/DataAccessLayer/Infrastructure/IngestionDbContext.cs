using CoreModels.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Infrastructure;

public sealed class IngestionDbContext(DbContextOptions<IngestionDbContext> options) : DbContext(options)
{
    public DbSet<Upload> Uploads => Set<Upload>();
    public DbSet<UploadTransaction> UploadTransactions => Set<UploadTransaction>();
    public DbSet<IngestionSheet> Sheets => Set<IngestionSheet>();
    public DbSet<IngestionColumn> Columns => Set<IngestionColumn>();
    public DbSet<SourceColumn> SourceColumns => Set<SourceColumn>();
    public DbSet<RowMetadataValue> RowMetadataValues => Set<RowMetadataValue>();
    public DbSet<IngestionRow> Rows => Set<IngestionRow>();
    public DbSet<DuplicateAudit> DuplicateAudits => Set<DuplicateAudit>();
    public DbSet<ValidationAudit> ValidationAudits => Set<ValidationAudit>();
    public DbSet<AppUser> Users => Set<AppUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Upload>().HasKey(x => x.UploadId);
        modelBuilder.Entity<Upload>().HasIndex(x => new { x.UserId, x.UploadedOn });
        modelBuilder.Entity<Upload>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<UploadTransaction>().HasKey(x => x.TransactionId);
        modelBuilder.Entity<UploadTransaction>().ToTable("UploadTransactionsDetails");
        modelBuilder.Entity<IngestionSheet>().HasKey(x => x.SheetId);
        modelBuilder.Entity<IngestionSheet>().HasIndex(x => new { x.UploadId, x.SheetOrder });
        modelBuilder.Entity<IngestionColumn>().HasKey(x => x.ColumnId);
        modelBuilder.Entity<IngestionColumn>().HasIndex(x => new { x.SheetId, x.ColumnOrder });
        modelBuilder.Entity<SourceColumn>().HasKey(x => x.SourceColumnId);
        modelBuilder.Entity<SourceColumn>().HasIndex(x => x.ColumnId).IsUnique();
        modelBuilder.Entity<SourceColumn>().HasOne(x => x.Column).WithMany(x => x.SourceColumns).HasForeignKey(x => x.ColumnId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<RowMetadataValue>().HasKey(x => x.RowMetadataValueId);
        modelBuilder.Entity<RowMetadataValue>().HasIndex(x => new { x.RowId, x.SourceColumnId }).IsUnique();
        modelBuilder.Entity<RowMetadataValue>().HasOne(x => x.Row).WithMany(x => x.MetadataValues).HasForeignKey(x => x.RowId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<RowMetadataValue>().HasOne(x => x.SourceColumn).WithMany(x => x.MetadataValues).HasForeignKey(x => x.SourceColumnId).OnDelete(DeleteBehavior.NoAction);
        modelBuilder.Entity<IngestionRow>().HasKey(x => x.RowId);
        modelBuilder.Entity<IngestionRow>().HasIndex(x => new { x.SheetId, x.RowNumber });
        modelBuilder.Entity<IngestionRow>().Property(x => x.CustomerQty).HasPrecision(18, 4);
        modelBuilder.Entity<DuplicateAudit>().HasKey(x => x.AuditId);
        modelBuilder.Entity<ValidationAudit>().HasKey(x => x.ValidationId);
        modelBuilder.Entity<AppUser>().HasKey(x => x.UserId);
        modelBuilder.Entity<AppUser>().HasIndex(x => x.UserName).IsUnique();
        modelBuilder.Entity<AppUser>().Property(x => x.IsActive).HasDefaultValue(true);
    }
}
