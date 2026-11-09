using System.ComponentModel.DataAnnotations;

namespace GalaReconciler.Web.Features.Reconciliation;

/// <summary>An uploaded file must be a non-empty .xlsx no larger than <see cref="UploadFilesViewModel.MaxFileBytes"/>.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class XlsxFileAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        // Presence is [Required]'s job.
        if (value is not IFormFile file)
        {
            return ValidationResult.Success;
        }

        var name = validationContext.DisplayName;
        var members = validationContext.MemberName is { } member ? new[] { member } : null;

        if (!string.Equals(Path.GetExtension(file.FileName), UploadFilesViewModel.AllowedExtension, StringComparison.OrdinalIgnoreCase))
        {
            return new ValidationResult($"The {name} must be an .xlsx file.", members);
        }

        if (file.Length == 0)
        {
            return new ValidationResult($"The {name} is empty.", members);
        }

        if (file.Length > UploadFilesViewModel.MaxFileBytes)
        {
            return new ValidationResult($"The {name} must be 10 MB or smaller.", members);
        }

        return ValidationResult.Success;
    }
}
