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
[Migration("20251014153909_Init")]
public class Init : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.CreateTable("Squads", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("TEXT");
			int? maxLength = 50;
			return new
			{
				Id = id,
				Name = table.Column<string>("TEXT", null, maxLength)
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_Squads", x => x.Id);
		});
		migrationBuilder.CreateTable("Accounts", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("TEXT");
			int? maxLength = 50;
			OperationBuilder<AddColumnOperation> name = table.Column<string>("TEXT", null, maxLength);
			maxLength = 100;
			OperationBuilder<AddColumnOperation> email = table.Column<string>("TEXT", null, maxLength, rowVersion: false, null, nullable: true);
			maxLength = 256;
			return new
			{
				Id = id,
				Name = name,
				Email = email,
				ImageSource = table.Column<string>("TEXT", null, maxLength, rowVersion: false, null, nullable: true),
				SquadId = table.Column<Guid>("TEXT")
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_Accounts", x => x.Id);
			table.ForeignKey("FK_Accounts_Squads_SquadId", x => x.SquadId, "Squads", "Id", null, ReferentialAction.NoAction, ReferentialAction.Cascade);
		});
		migrationBuilder.CreateIndex("IX_Accounts_SquadId", "Accounts", "SquadId");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropTable("Accounts");
		migrationBuilder.DropTable("Squads");
	}

	protected override void BuildTargetModel(ModelBuilder modelBuilder)
	{
		modelBuilder.HasAnnotation("ProductVersion", "9.0.0");
		modelBuilder.Entity("Pw.Hub.Models.Account", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").HasColumnType("TEXT");
			b.Property<string>("Email").HasMaxLength(100).HasColumnType("TEXT");
			b.Property<string>("ImageSource").HasMaxLength(256).HasColumnType("TEXT");
			b.Property<string>("Name").IsRequired().HasMaxLength(50)
				.HasColumnType("TEXT");
			b.Property<Guid>("SquadId").HasColumnType("TEXT");
			b.HasKey("Id");
			b.HasIndex("SquadId");
			b.ToTable("Accounts");
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
		modelBuilder.Entity("Pw.Hub.Models.Squad", delegate(EntityTypeBuilder b)
		{
			b.Navigation("Accounts");
		});
	}
}
