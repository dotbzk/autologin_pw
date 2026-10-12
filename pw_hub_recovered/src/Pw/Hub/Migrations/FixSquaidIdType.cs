using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Migrations;
using Pw.Hub.Infrastructure;

namespace Pw.Hub.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20251025014238_FixSquaidIdType")]
public class FixSquaidIdType : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		Type typeFromHandle = typeof(Guid);
		migrationBuilder.AlterColumn<string>("SquadId", "Accounts", "TEXT", null, null, rowVersion: false, null, nullable: true, null, null, null, typeFromHandle, "TEXT");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		object defaultValue = new Guid("00000000-0000-0000-0000-000000000000");
		Type typeFromHandle = typeof(string);
		migrationBuilder.AlterColumn<Guid>("SquadId", "Accounts", "TEXT", null, null, rowVersion: false, null, nullable: false, defaultValue, null, null, typeFromHandle, "TEXT", null, null, oldRowVersion: false, oldNullable: true);
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
			b.Property<int>("OrderIndex").ValueGeneratedOnAdd().HasColumnType("INTEGER")
				.HasDefaultValue(0);
			b.Property<string>("SiteId").HasMaxLength(100).HasColumnType("TEXT");
			b.Property<string>("SquadId").HasColumnType("TEXT");
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
			b.Property<string>("Id").HasColumnType("TEXT");
			b.Property<string>("Name").IsRequired().HasMaxLength(50)
				.HasColumnType("TEXT");
			b.Property<int>("OrderIndex").ValueGeneratedOnAdd().HasColumnType("INTEGER")
				.HasDefaultValue(0);
			b.HasKey("Id");
			b.ToTable("Squads");
		});
		modelBuilder.Entity("Pw.Hub.Models.Account", delegate(EntityTypeBuilder b)
		{
			b.HasOne("Pw.Hub.Models.Squad", "Squad").WithMany("Accounts").HasForeignKey("SquadId")
				.OnDelete(DeleteBehavior.Cascade);
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
