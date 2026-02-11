namespace DM.Services.Core.Exceptions;

/// <summary>
/// Set of server validation errors
/// </summary>
public static class ValidationError
{
    /// <summary>
    /// Field must not be null or default or whitespace string
    /// </summary>
    public const string Empty = nameof(Empty);

    /// <summary>
    /// The field value is too short
    /// </summary>
    public const string Short = nameof(Short);

    /// <summary>
    /// The field value is too long
    /// </summary>
    public const string Long = nameof(Long);

    /// <summary>
    /// The field value is already taken and requires to be unique
    /// </summary>
    public const string Taken = nameof(Taken);

    /// <summary>
    /// Field value does not match some pattern, e.g. email or regex
    /// </summary>
    public const string Invalid = nameof(Invalid);

    /// <summary>
    /// Password requires at least one uppercase letter
    /// </summary>
    public const string RequiresUppercase = nameof(RequiresUppercase);

    /// <summary>
    /// Password requires at least one lowercase letter
    /// </summary>
    public const string RequiresLowercase = nameof(RequiresLowercase);

    /// <summary>
    /// Password requires at least one digit
    /// </summary>
    public const string RequiresDigit = nameof(RequiresDigit);

    /// <summary>
    /// Password requires at least one special character
    /// </summary>
    public const string RequiresSpecialCharacter = nameof(RequiresSpecialCharacter);

    /// <summary>
    /// The collection has too many items
    /// </summary>
    public const string TooMany = nameof(TooMany);

    /// <summary>
    /// The date/time value must be in the future
    /// </summary>
    public const string MustBeFuture = nameof(MustBeFuture);

    /// <summary>
    /// The numeric value must be positive
    /// </summary>
    public const string MustBePositive = nameof(MustBePositive);

    /// <summary>
    /// The field value is unchanged from the current value
    /// </summary>
    public const string Unchanged = nameof(Unchanged);
}