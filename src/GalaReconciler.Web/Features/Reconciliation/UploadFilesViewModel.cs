using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace GalaReconciler.Web.Features.Reconciliation;

public sealed class UploadFilesViewModel
{
    public const long MaxFileBytes = 10 * 1024 * 1024;

    /// <summary>Both files plus room for the multipart envelope and antiforgery token.</summary>
    public const long MaxRequestBytes = (2 * MaxFileBytes) + (1024 * 1024);

    public const string AllowedExtension = ".xlsx";

    [Display(Name = "Source file (RSVP export)")]
    [Required(ErrorMessage = "Choose the Source file (RSVP export).")]
    [XlsxFile]
    public IFormFile? SourceFile { get; set; }

    [Display(Name = "Processed file (master guest list)")]
    [Required(ErrorMessage = "Choose the Processed file (master guest list).")]
    [XlsxFile]
    public IFormFile? ProcessedFile { get; set; }

    /// <summary>A problem found while reading a file (e.g. missing headers), shown above the form.</summary>
    [BindNever]
    public UploadError? Error { get; set; }
}

public sealed record UploadError(string Title, string Message, IReadOnlyList<string> MissingHeaders);
