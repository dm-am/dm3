using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Http;

namespace DM.Web.API.Validation;

/// <inheritdoc />
internal class FileContentTypeValidationAttribute : ValidationAttribute
{
    private readonly HashSet<string> _contentTypes;

    /// <inheritdoc />
    public FileContentTypeValidationAttribute(params string[] contentTypes)
    {
        _contentTypes = contentTypes.ToHashSet();
    }

    /// <inheritdoc />
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext) => value switch
    {
        IFormFileCollection files => files.All(IsValid)
            ? ValidationResult.Success
            : new ValidationResult(ErrorMessage),
        IFormFile file => IsValid(file)
            ? ValidationResult.Success
            : new ValidationResult(ErrorMessage),
        _ => new ValidationResult("Value must be a file")
    };

    private bool IsValid(IFormFile file) => _contentTypes.Contains(file.ContentType);
}