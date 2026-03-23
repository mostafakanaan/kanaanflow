namespace KanaanFlow.Tests;

using KanaanFlow.Core.Models;
using System;
using Xunit;

public class CategoryModelTests
{
    [Fact]
    public void Category_DefaultValues_AreEmpty()
    {
        Category category = new Category();

        Assert.Equal(Guid.Empty, category.Id);
        Assert.Equal(string.Empty, category.Name);
        Assert.Equal(string.Empty, category.Icon);
        Assert.Equal(string.Empty, category.Color);
    }

    [Fact]
    public void Category_WithValues_StoresCorrectly()
    {
        Guid id = Guid.NewGuid();
        Category category = new Category
        {
            Id = id,
            Name = "Food",
            Icon = "🍔",
            Color = "#FF6B6B"
        };

        Assert.Equal(id, category.Id);
        Assert.Equal("Food", category.Name);
        Assert.Equal("🍔", category.Icon);
        Assert.Equal("#FF6B6B", category.Color);
    }

    [Fact]
    public void Category_NameProperty_CanBeUpdated()
    {
        Category category = new Category { Name = "Old Name" };
        category.Name = "New Name";

        Assert.Equal("New Name", category.Name);
    }
}
