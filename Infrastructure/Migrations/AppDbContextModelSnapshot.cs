using System;
using Core.Models.Entities;
using Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
public class AppDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder
            .HasAnnotation("ProductVersion", "9.0.9")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);

        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

        modelBuilder.Entity("Core.Models.Entities.Food", b =>
        {
            b.Property<int>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("integer");

            NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

            b.Property<string>("Brands")
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnType("character varying(255)");

            b.Property<int?>("Calories")
                .HasColumnType("integer");

            b.Property<double>("Carbohydrates")
                .HasColumnType("double precision");

            b.Property<double>("Fat")
                .HasColumnType("double precision");

            b.Property<int>("MealId")
                .HasColumnType("integer");

            b.Property<string>("Name")
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnType("character varying(255)");

            b.Property<double>("Proteins")
                .HasColumnType("double precision");

            b.Property<double>("Quantity")
                .HasColumnType("double precision");

            b.HasKey("Id");

            b.HasIndex("MealId");

            b.ToTable("MealItems");
        });

        modelBuilder.Entity("Core.Models.Entities.Meal", b =>
        {
            b.Property<int>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("integer");

            NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

            b.Property<DateTime>("CreatedAt")
                .HasColumnType("timestamp with time zone");

            b.Property<int>("UserId")
                .HasColumnType("integer");

            b.HasKey("Id");

            b.HasIndex("UserId");

            b.ToTable("Meals");
        });

        modelBuilder.Entity("Core.Models.Entities.Token", b =>
        {
            b.Property<int>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("integer");

            NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

            b.Property<DateTime>("ExpiresAt")
                .HasColumnType("timestamp with time zone");

            b.Property<DateTime?>("RevokedAt")
                .HasColumnType("timestamp with time zone");

            b.Property<string>("TokenFingerprint")
                .IsRequired()
                .HasMaxLength(64)
                .HasColumnType("character varying(64)");

            b.Property<string>("TokenHash")
                .IsRequired()
                .HasMaxLength(512)
                .HasColumnType("character varying(512)");

            b.Property<int>("UserId")
                .HasColumnType("integer");

            b.HasKey("Id");

            b.HasIndex("UserId");

            b.HasIndex("TokenFingerprint")
                .IsUnique();

            b.ToTable("RefreshTokens");
        });

        modelBuilder.Entity("Core.Models.Entities.User", b =>
        {
            b.Property<int>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("integer");

            NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

            b.Property<DateTime>("CreatedAt")
                .HasColumnType("timestamp with time zone");

            b.Property<string>("PasswordHash")
                .IsRequired()
                .HasMaxLength(512)
                .HasColumnType("character varying(512)");

            b.Property<string>("Username")
                .IsRequired()
                .HasMaxLength(64)
                .HasColumnType("character varying(64)");

            b.HasKey("Id");

            b.HasIndex("Username")
                .IsUnique();

            b.ToTable("Users");
        });

        modelBuilder.Entity("Core.Models.Entities.Food", b =>
        {
            b.HasOne("Core.Models.Entities.Meal", "Meal")
                .WithMany("Foods")
                .HasForeignKey("MealId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.Navigation("Meal");
        });

        modelBuilder.Entity("Core.Models.Entities.Meal", b =>
        {
            b.HasOne("Core.Models.Entities.User", "User")
                .WithMany("Meals")
                .HasForeignKey("UserId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.Navigation("User");
        });

        modelBuilder.Entity("Core.Models.Entities.Token", b =>
        {
            b.HasOne("Core.Models.Entities.User", "User")
                .WithMany("RefreshTokens")
                .HasForeignKey("UserId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.Navigation("User");
        });

        modelBuilder.Entity("Core.Models.Entities.Meal", b =>
        {
            b.Navigation("Foods");
        });

        modelBuilder.Entity("Core.Models.Entities.User", b =>
        {
            b.Navigation("Meals");

            b.Navigation("RefreshTokens");
        });
#pragma warning restore 612, 618
    }
}
