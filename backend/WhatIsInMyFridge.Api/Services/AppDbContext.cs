using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using WhatIsInMyFridge.Api.Models;

namespace WhatIsInMyFridge.Api.Services;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Household> Households { get; set; }
    public DbSet<FoodItem> FoodItems { get; set; }
    public DbSet<Recipe> Recipes { get; set; }
    // RecipeIngredient is owned by Recipe - not a standalone entity
    // public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<GroceryItem> GroceryItems { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Cosmos DB: Configure containers and partition keys
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToContainer("Users");
            entity.HasPartitionKey(e => e.Id);
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).IsRequired();
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.PasswordHash).IsRequired();
        });

        modelBuilder.Entity<Household>(entity =>
        {
            entity.ToContainer("Households");
            entity.HasPartitionKey(e => e.Id);
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.OwnerId).IsRequired();
        });

        modelBuilder.Entity<FoodItem>(entity =>
        {
            entity.ToContainer("FoodItems");
            entity.HasPartitionKey(e => e.HouseholdId);
            entity.HasKey(e => e.Id);
            entity.Property(e => e.HouseholdId).IsRequired();
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.Unit)
                .IsRequired()
                .HasConversion<string>();
            entity.Property(e => e.Category)
                .HasConversion<string>();
        });

        modelBuilder.Entity<Recipe>(entity =>
        {
            entity.ToContainer("Recipes");
            entity.HasPartitionKey(e => e.HouseholdId);
            entity.HasKey(e => e.Id);
            entity.Property(e => e.HouseholdId).IsRequired();
            entity.Property(e => e.Name).IsRequired();
            // Ingredients are stored inline as owned entities
            entity.OwnsMany(e => e.Ingredients, ingredient =>
            {
                ingredient.Property(i => i.Unit).HasConversion<string>();
            });
        });

        // RecipeIngredients container is not used - ingredients are stored inline with Recipe
        // Commenting out to avoid conflict with OwnsMany above
        /*
        modelBuilder.Entity<RecipeIngredient>(entity =>
        {
            entity.ToContainer("RecipeIngredients");
            entity.HasPartitionKey(e => e.RecipeId);
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RecipeId).IsRequired();
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.Unit)
                .IsRequired()
                .HasConversion<string>();
        });
        */

        modelBuilder.Entity<GroceryItem>(entity =>
        {
            entity.ToContainer("GroceryItems");
            entity.HasPartitionKey(e => e.HouseholdId);
            entity.HasKey(e => e.Id);
            entity.Property(e => e.HouseholdId).IsRequired();
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.Category)
                .HasConversion<string>();
        });
    }
}
