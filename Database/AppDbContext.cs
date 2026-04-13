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
        // Конфигурация связей ConstructionObject -> Act
        modelBuilder.Entity<ConstructionObject>()
            .HasMany(co => co.Acts)
            .WithOne(a => a.ConstructionObject)
            .HasForeignKey(a => a.ConstructionObjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // Конфигурация связей ConstructionObject -> Employee
        modelBuilder.Entity<ConstructionObject>()
            .HasMany(co => co.Employees)
            .WithOne(e => e.ConstructionObject)
            .HasForeignKey(e => e.ConstructionObjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // Конфигурация связей ConstructionObject -> Material
        modelBuilder.Entity<ConstructionObject>()
            .HasMany(co => co.Materials)
            .WithOne(m => m.ConstructionObject)
            .HasForeignKey(m => m.ConstructionObjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // Конфигурация связей ConstructionObject -> Schema
        modelBuilder.Entity<ConstructionObject>()
            .HasMany(co => co.Schemas)
            .WithOne(s => s.ConstructionObject)
            .HasForeignKey(s => s.ConstructionObjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // Конфигурация связей ConstructionObject -> Protocol
        modelBuilder.Entity<ConstructionObject>()
            .HasMany(co => co.Protocols)
            .WithOne(p => p.ConstructionObject)
            .HasForeignKey(p => p.ConstructionObjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // Конфигурация связей ConstructionObject -> ProjectDoc
        modelBuilder.Entity<ConstructionObject>()
            .HasMany(co => co.ProjectDocs)
            .WithOne(pd => pd.ConstructionObject)
            .HasForeignKey(pd => pd.ConstructionObjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // Конфигурация связей Act -> Protocol
        modelBuilder.Entity<Act>()
            .HasMany(a => a.Protocols)
            .WithOne(p => p.Act)
            .HasForeignKey(p => p.ActId)
            .OnDelete(DeleteBehavior.SetNull);

        // Промежуточная таблица Act-Material
        modelBuilder.Entity<ActMaterial>()
            .HasOne(am => am.Act)
            .WithMany(a => a.ActMaterials)
            .HasForeignKey(am => am.ActId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ActMaterial>()
            .HasOne(am => am.Material)
            .WithMany(m => m.ActMaterials)
            .HasForeignKey(am => am.MaterialId)
            .OnDelete(DeleteBehavior.Restrict);

        // Промежуточная таблица Act-Schema
        modelBuilder.Entity<ActSchema>()
            .HasOne(asc => asc.Act)
            .WithMany(a => a.ActSchemas)
            .HasForeignKey(asc => asc.ActId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ActSchema>()
            .HasOne(asc => asc.Schema)
            .WithMany(s => s.ActSchemas)
            .HasForeignKey(asc => asc.SchemaId)
            .OnDelete(DeleteBehavior.Restrict);

        // Промежуточная таблица Act-ProjectDoc
        modelBuilder.Entity<ActProjectDoc>()
            .HasOne(apd => apd.Act)
            .WithMany(a => a.ActProjectDocs)
            .HasForeignKey(apd => apd.ActId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ActProjectDoc>()
            .HasOne(apd => apd.ProjectDoc)
            .WithMany(pd => pd.ActProjectDocs)
            .HasForeignKey(apd => apd.ProjectDocId)
            .OnDelete(DeleteBehavior.Restrict);

        // Индексы для производительности
        modelBuilder.Entity<Act>()
            .HasIndex(a => a.ConstructionObjectId);

        modelBuilder.Entity<Act>()
            .HasIndex(a => a.Type);

        // Связи Act -> Act (ИД и АООК)
        modelBuilder.Entity<Act>()
            .HasOne(a => a.RelatedAct)
            .WithMany()
            .HasForeignKey(a => a.RelatedActId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Act>()
            .HasOne(a => a.RelatedAook)
            .WithMany()
            .HasForeignKey(a => a.RelatedAookId)
            .OnDelete(DeleteBehavior.Restrict);

        // Связи Act -> Employee (подписанты)
        modelBuilder.Entity<Act>()
            .HasOne(a => a.CustomerRep)
            .WithMany()
            .HasForeignKey(a => a.CustomerRepId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Act>()
            .HasOne(a => a.GenContractorRep)
            .WithMany()
            .HasForeignKey(a => a.GenContractorRepId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Act>()
            .HasOne(a => a.ContractorRep)
            .WithMany()
            .HasForeignKey(a => a.ContractorRepId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Act>()
            .HasOne(a => a.DesignerRep)
            .WithMany()
            .HasForeignKey(a => a.AuthorSupervisionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Act>()
            .HasOne(a => a.GenContractorSkRep)
            .WithMany()
            .HasForeignKey(a => a.GenContractorSkRepId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Act>()
            .HasOne(a => a.OtherPerson1)
            .WithMany()
            .HasForeignKey(a => a.OtherPerson1Id)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Act>()
            .HasOne(a => a.OtherPerson2)
            .WithMany()
            .HasForeignKey(a => a.OtherPerson2Id)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Act>()
            .HasOne(a => a.OtherPerson3)
            .WithMany()
            .HasForeignKey(a => a.OtherPerson3Id)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Employee>()
            .HasIndex(e => e.ConstructionObjectId);

        modelBuilder.Entity<Material>()
            .HasIndex(m => m.ConstructionObjectId);

        modelBuilder.Entity<Schema>()
            .HasIndex(s => s.ConstructionObjectId);

        modelBuilder.Entity<ProjectDoc>()
            .HasIndex(pd => pd.ConstructionObjectId);

        base.OnModelCreating(modelBuilder);
    }
}
