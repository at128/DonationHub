
using DonationHub.Domain.Common;

namespace DonationHub.Domain.Entities;

public class Category : AuditableEntity
{
    public string NameAr { get; private set; } = null!;
    public string NameEn { get; private set; } = null!;
    public string DescriptionAr { get; private set; } = null!;
    public string DescriptionEn { get; private set; } = null!;

    private Category() { }

    private Category(Guid id) : base(id) { }

    public static Category Create(
        string nameAr,
        string nameEn,
        string descriptionAr,
        string descriptionEn)
    {
        nameAr = ValidateName(nameAr);
        nameEn = ValidateName(nameEn);
        descriptionAr = ValidateDescription(descriptionAr);
        descriptionEn = ValidateDescription(descriptionEn);

        return new Category(Guid.CreateVersion7())
        {
            NameAr = nameAr,
            NameEn = nameEn,
            DescriptionAr = descriptionAr,
            DescriptionEn = descriptionEn
        };
    }

    public void Update(
        string nameAr,
        string nameEn,
        string descriptionAr,
        string descriptionEn)
    {
        nameAr = ValidateName(nameAr);
        nameEn = ValidateName(nameEn);
        descriptionAr = ValidateDescription(descriptionAr);
        descriptionEn = ValidateDescription(descriptionEn);

        NameAr = nameAr;
        NameEn = nameEn;
        DescriptionAr = descriptionAr;
        DescriptionEn = descriptionEn;
    }

    private static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Category name cannot be empty.");
        }

        name = name.Trim();

        if (name.Length > 100)
        {
            throw new ArgumentException(
                "Category name cannot exceed 100 characters.");
        }

        return name;
    }

    private static string ValidateDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException(
                "Category description cannot be empty.");
        }

        description = description.Trim();

        if (description.Length > 500)
        {
            throw new ArgumentException(
                "Category description cannot exceed 500 characters.");
        }

        return description;
    }
}
