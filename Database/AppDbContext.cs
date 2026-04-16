using System;
using System.IO;
using AGenerator.Models;
using Microsoft.EntityFrameworkCore;

namespace AGenerator.Database;

public class AppDbContext : DbContext
{
    public DbSet<ConstructionObject> Objects { get; set; }
    public DbSet<Act> Acts { get; set; }
    public DbSet<Employee> Employees { get; set; }
    public DbSet<Material> Materials { get; set; }
    public DbSet<Schema> Schemas { get; set; }
    public DbSet<Protocol> Protocols { get; set; }
    public DbSet<ActMaterial> ActMaterials { get; set; }
    public DbSet<ActSchema> ActSchemas { get; set; }
    public DbSet<ActProjectDoc> ActProjectDocs { get; set; }
    public DbSet<ProjectDoc> ProjectDocs { get; set; }
    public DbSet<Organization> Organizations { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "agenerator.db");
        optionsBuilder.UseSqlite($"Data Source={dbPath}");

#if DEBUG
        optionsBuilder.EnableSensitiveDataLogging();
        optionsBuilder.LogTo(Console.WriteLine);
#endif
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ==================== CONSTRUCTION OBJECT → ORGANIZATIONS (one-to-many) ====================
        modelBuilder.Entity<Organization>()
            .HasOne(o => o.ConstructionObject)
            .WithMany(c => c.Organizations)
            .HasForeignKey(o => o.ConstructionObjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // ==================== ACT → ORGANIZATION (snapshot) ====================
        modelBuilder.Entity<Act>()
            .HasOne(a => a.CustomerOrganization)
            .WithMany()
            .HasForeignKey(a => a.CustomerOrganizationId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Act>()
            .HasOne(a => a.GenContractorOrganization)
            .WithMany()
            .HasForeignKey(a => a.GenContractorOrganizationId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Act>()
            .HasOne(a => a.ContractorOrganization)
            .WithMany()
            .HasForeignKey(a => a.ContractorOrganizationId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Act>()
            .HasOne(a => a.DesignerOrganization)
            .WithMany()
            .HasForeignKey(a => a.DesignerOrganizationId)
            .OnDelete(DeleteBehavior.SetNull);

        // ==================== CONSTRUCTION OBJECT → DEFAULT ORGANIZATION (legacy) ====================
        modelBuilder.Entity<ConstructionObject>()
            .HasOne(o => o.DefaultCustomerOrganization)
            .WithMany()
            .HasForeignKey(o => o.DefaultCustomerOrganizationId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ConstructionObject>()
            .HasOne(o => o.DefaultGenContractorOrganization)
            .WithMany()
            .HasForeignKey(o => o.DefaultGenContractorOrganizationId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ConstructionObject>()
            .HasOne(o => o.DefaultContractorOrganization)
            .WithMany()
            .HasForeignKey(o => o.DefaultContractorOrganizationId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ConstructionObject>()
            .HasOne(o => o.DefaultDesignerOrganization)
            .WithMany()
            .HasForeignKey(o => o.DefaultDesignerOrganizationId)
            .OnDelete(DeleteBehavior.SetNull);

        // ==================== ACT → RELATED ACTS ====================
        modelBuilder.Entity<Act>()
            .HasOne(a => a.RelatedAct)
            .WithMany()
            .HasForeignKey(a => a.RelatedActId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Act>()
            .HasOne(a => a.RelatedAook)
            .WithMany()
            .HasForeignKey(a => a.RelatedAookId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
