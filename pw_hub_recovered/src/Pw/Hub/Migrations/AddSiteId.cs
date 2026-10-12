using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Migrations;
using Pw.Hub.Infrastructure;

namespace Pw.Hub.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20251023185850_AddSiteId")]
public class AddSiteId : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.RenameColumn("Email", "Accounts", "SiteId");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.RenameColumn("SiteId", "Accounts", "Email");
	}

	protected override void BuildTargetModel(ModelBuilder modelBuilder)
	{
		modelBuilder.HasAnnotation("ProductVersion", "9.0.0");
		modelBuilder.Entity("Pw.Hub.Models.Account", delegate(EntityTypeBuilder b)
		{
			b.Property<string>("Id").HasColumnType("TEXT");
			b.Property<string>("ImageSource").HasMaxLength(256).HasColumnType("TEXT");
			b.Property<DateTime>("LastVisit").HasColumnType("TEXT");
			b.Property<string>("Name").IsRequired().HasMaxLength(50)
				.HasColumnType("TEXT");
			b.Property<string>("SiteId").HasMaxLength(100).HasColumnType("TEXT");
			b.Property<Guid>("SquadId").HasColumnType("TEXT");
			b.HasKey("Id");
			b.HasIndex("SquadId");
			b.ToTable("Accounts");
		});
		modelBuilder.Entity("Pw.Hub.Models.AccountCharacter", delegate(EntityTypeBuilder b)
		{
			b.Property<string>("Id").HasColumnType("TEXT");
			b.Property<string>("Name").IsRequired().HasMaxLength(50)
				.HasColumnType("TEXT");
			b.Property<string>("OptionId").IsRequired().HasMaxLength(50)
				.HasColumnType("TEXT");
			b.Property<string>("ServerId").HasColumnType("TEXT");
			b.HasKey("Id");
			b.HasIndex("ServerId");
			b.ToTable("AccountCharacter");
		});
		modelBuilder.Entity("Pw.Hub.Models.AccountServer", delegate(EntityTypeBuilder b)
		{
			b.Property<string>("Id").HasColumnType("TEXT");
			b.Property<string>("AccountId").HasColumnType("TEXT");
			b.Property<string>("DefaultCharacterOptionId").HasMaxLength(50).HasColumnType("TEXT");
			b.Property<string>("Name").IsRequired().HasMaxLength(50)
				.HasColumnType("TEXT");
			b.Property<string>("OptionId").IsRequired().HasMaxLength(50)
				.HasColumnType("TEXT");
			b.HasKey("Id");
			b.HasIndex("AccountId");
			b.ToTable("AccountServer");
		});
		modelBuilder.Entity("Pw.Hub.Models.Squad", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").HasColumnType("TEXT");
			b.Property<string>("Name").IsRequired().HasMaxLength(50)
				.HasColumnType("TEXT");
			b.HasKey("Id");
			b.ToTable("Squads");
		});
		modelBuilder.Entity("Pw.Hub.Models.Account", delegate(EntityTypeBuilder b)
		{
			b.HasOne("Pw.Hub.Models.Squad", "Squad").WithMany("Accounts").HasForeignKey("SquadId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("Squad");
		});
		modelBuilder.Entity("Pw.Hub.Models.AccountCharacter", delegate(EntityTypeBuilder b)
		{
			b.HasOne("Pw.Hub.Models.AccountServer", "Server").WithMany("Characters").HasForeignKey("ServerId")
				.OnDelete(DeleteBehavior.Cascade);
			b.Navigation("Server");
		});
		modelBuilder.Entity("Pw.Hub.Models.AccountServer", delegate(EntityTypeBuilder b)
		{
			b.HasOne("Pw.Hub.Models.Account", "Account").WithMany("Servers").HasForeignKey("AccountId")
				.OnDelete(DeleteBehavior.Cascade);
			b.Navigation("Account");
		});
		modelBuilder.Entity("Pw.Hub.Models.Account", delegate(EntityTypeBuilder b)
		{
			b.Navigation("Servers");
		});
		modelBuilder.Entity("Pw.Hub.Models.AccountServer", delegate(EntityTypeBuilder b)
		{
			b.Navigation("Characters");
		});
		modelBuilder.Entity("Pw.Hub.Models.Squad", delegate(EntityTypeBuilder b)
		{
			b.Navigation("Accounts");
		});
	}
}
