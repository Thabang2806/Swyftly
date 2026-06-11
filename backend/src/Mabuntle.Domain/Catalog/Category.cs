using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Catalog;

public sealed class Category : Entity
{
    private Category()
    {
    }

    public Category(
        Guid? parentCategoryId,
        string name,
        string slug,
        int displayOrder = 0,
        bool isActive = true)
        : this(Guid.NewGuid(), parentCategoryId, name, slug, displayOrder, isActive)
    {
    }

    public Category(
        Guid id,
        Guid? parentCategoryId,
        string name,
        string slug,
        int displayOrder = 0,
        bool isActive = true)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Category id is required.", nameof(id));
        }

        if (parentCategoryId == id)
        {
            throw new ArgumentException("A category cannot be its own parent.", nameof(parentCategoryId));
        }

        Id = id;
        ParentCategoryId = parentCategoryId;
        Name = Required(name, nameof(name));
        Slug = NormalizeSlug(slug);
        DisplayOrder = NonNegative(displayOrder, nameof(displayOrder));
        IsActive = isActive;
    }

    public Guid? ParentCategoryId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public int DisplayOrder { get; private set; }

    public bool IsActive { get; private set; }

    public void Update(
        Guid? parentCategoryId,
        string name,
        string slug,
        int displayOrder)
    {
        if (parentCategoryId == Id)
        {
            throw new ArgumentException("A category cannot be its own parent.", nameof(parentCategoryId));
        }

        ParentCategoryId = parentCategoryId;
        Name = Required(name, nameof(name));
        Slug = NormalizeSlug(slug);
        DisplayOrder = NonNegative(displayOrder, nameof(displayOrder));
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    private static string Required(string? value, string parameterName)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return trimmed;
    }

    private static int NonNegative(int value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Value cannot be negative.");
        }

        return value;
    }

    private static string NormalizeSlug(string slug)
    {
        var normalized = Required(slug, nameof(slug)).Trim().ToLowerInvariant();
        if (normalized.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
        {
            throw new ArgumentException("Slug can only contain letters, numbers, and hyphens.", nameof(slug));
        }

        return normalized;
    }
}
