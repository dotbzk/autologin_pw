using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pw.Hub.Models;

namespace Pw.Hub.Infrastructure;

public class AppDbContext : DbContext
{
	public DbSet<Squad> Squads { get; set; }

	public DbSet<Account> Accounts { get; set; }

	protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
	{
		optionsBuilder.UseSqlite("Data Source=pwhub.db");
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity(delegate(EntityTypeBuilder<Squad> entity)
		{
			entity.HasKey((Squad e) => e.Id);
			entity.Property((Squad e) => e.Id).ValueGeneratedNever();
			entity.Property((Squad e) => e.Name).IsRequired().HasMaxLength(50);
			entity.Property<int>("OrderIndex").HasDefaultValue(0);
			entity.HasMany((Squad e) => e.Accounts).WithOne((Account e) => e.Squad).HasForeignKey((Account e) => e.SquadId)
				.OnDelete(DeleteBehavior.Cascade);
		});
		modelBuilder.Entity(delegate(EntityTypeBuilder<Account> entity)
		{
			entity.HasKey((Account e) => e.Id);
			entity.Property((Account e) => e.Id).ValueGeneratedNever();
			entity.Property((Account e) => e.Name).IsRequired().HasMaxLength(50);
			entity.Property((Account e) => e.SiteId).HasMaxLength(100);
			entity.Property((Account e) => e.ImageSource).HasMaxLength(256);
			entity.Property((Account e) => e.LastVisit);
			entity.Property((Account e) => e.OrderIndex).HasDefaultValue(0);
			entity.HasMany((Account e) => e.Servers).WithOne((AccountServer e) => e.Account).HasForeignKey((AccountServer e) => e.AccountId)
				.OnDelete(DeleteBehavior.Cascade);
		});
		modelBuilder.Entity(delegate(EntityTypeBuilder<AccountServer> entity)
		{
			entity.HasKey((AccountServer e) => e.Id);
			entity.Property((AccountServer e) => e.Id).ValueGeneratedNever();
			entity.Property((AccountServer e) => e.OptionId).IsRequired().HasMaxLength(50);
			entity.Property((AccountServer e) => e.Name).IsRequired().HasMaxLength(50);
			entity.Property((AccountServer e) => e.DefaultCharacterOptionId).HasMaxLength(50);
			entity.HasMany((AccountServer e) => e.Characters).WithOne((AccountCharacter e) => e.Server).HasForeignKey((AccountCharacter e) => e.ServerId)
				.OnDelete(DeleteBehavior.Cascade);
		});
		modelBuilder.Entity(delegate(EntityTypeBuilder<AccountCharacter> entity)
		{
			entity.HasKey((AccountCharacter e) => e.Id);
			entity.Property((AccountCharacter e) => e.Id).ValueGeneratedNever();
			entity.Property((AccountCharacter e) => e.OptionId).IsRequired().HasMaxLength(50);
			entity.Property((AccountCharacter e) => e.Name).IsRequired().HasMaxLength(50);
		});
		base.OnModelCreating(modelBuilder);
	}
}
