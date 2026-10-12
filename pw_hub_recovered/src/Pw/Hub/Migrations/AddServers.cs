using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Migrations.Operations.Builders;
using Pw.Hub.Infrastructure;

namespace Pw.Hub.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20251022141245_AddServers")]
public class AddServers : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.CreateTable("AccountServer", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<string>("TEXT");
			int? maxLength = 50;
			OperationBuilder<AddColumnOperation> optionId = table.Column<string>("TEXT", null, maxLength);
			maxLength = 50;
			return new
			{
				Id = id,
				OptionId = optionId,
				Name = table.Column<string>("TEXT", null, maxLength),
				AccountId = table.Column<string>("TEXT", null, null, rowVersion: false, null, nullable: true)
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_AccountServer", x => x.Id);
			table.ForeignKey("FK_AccountServer_Accounts_AccountId", x => x.AccountId, "Accounts", "Id", null, ReferentialAction.NoAction, ReferentialAction.Cascade);
		});
		migrationBuilder.CreateTable("AccountCharacter", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<string>("TEXT");
			int? maxLength = 50;
			OperationBuilder<AddColumnOperation> optionId = table.Column<string>("TEXT", null, maxLength);
			maxLength = 50;
			return new
			{
				Id = id,
				OptionId = optionId,
				Name = table.Column<string>("TEXT", null, maxLength),
				ServerId = table.Column<string>("TEXT", null, null, rowVersion: false, null, nullable: true)
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_AccountCharacter", x => x.Id);
			table.ForeignKey("FK_AccountCharacter_AccountServer_ServerId", x => x.ServerId, "AccountServer", "Id", null, ReferentialAction.NoAction, ReferentialAction.Cascade);
		});
		migrationBuilder.CreateIndex("IX_AccountCharacter_ServerId", "AccountCharacter", "ServerId");
		migrationBuilder.CreateIndex("IX_AccountServer_AccountId", "AccountServer", "AccountId");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropTable("AccountCharacter");
		migrationBuilder.DropTable("AccountServer");
	}

	protected override void BuildTargetModel(ModelBuilder modelBuilder)
	{
		modelBuilder.HasAnnotation("ProductVersion", "9.0.0");
		modelBuilder.Entity("Pw.Hub.Models.Account", delegate(EntityTypeBuilder b)
		{
			b.Property<string>("Id").HasColumnType("TEXT");
			b.Property<string>("Email").HasMaxLength(100).HasColumnType("TEXT");
			b.Property<string>("ImageSource").HasMaxLength(256).HasColumnType("TEXT");
			b.Property<DateTime>("LastVisit").HasColumnType("TEXT");
			b.Property<string>("Name").IsRequired().HasMaxLength(50)
				.HasColumnType("TEXT");
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
