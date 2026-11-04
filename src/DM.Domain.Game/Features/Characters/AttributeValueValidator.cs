using DM.Domain.Core.Content;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.AttributeSchemas;
using System;
using System.Linq;
using DM.Domain.Core.Exceptions;

namespace DM.Domain.Game.Features.Characters;

/// <inheritdoc />
internal class AttributeValueValidator : IAttributeValueValidator
{
    /// <inheritdoc />
    public (bool valid, string? error) Validate(string value, AttributeSpecification specification)
    {
        if (value == null)
        {
            return (false, ValidationError.Empty);
        }

        var trimmedValue = value.Trim();
        if (string.IsNullOrEmpty(trimmedValue) && specification.Required)
        {
            return (false, AttributeValidationError.RequiredMissing);
        }

        // The Profile surface declares no [private], so the tag would hide nothing on the sheet.
        // Checked against the value as submitted, so the refusal points where the author looks.
        if (specification.Type == AttributeSpecificationType.BbCode &&
            PrivateBlockMarkup.ContainsPrivateMarkup(value))
        {
            return (false, PrivateBlockMarkup.DescribeSurfaceRefusal(value));
        }

        switch (specification.Type)
        {
            case AttributeSpecificationType.Number:
                if (!int.TryParse(trimmedValue, out var numberValue))
                {
                    return (false, AttributeValidationError.NotANumber);
                }
                if (specification.MaxLength.HasValue &&
                    Math.Abs(numberValue).ToString().Length > specification.MaxLength.Value)
                {
                    return (false, AttributeValidationError.StringTooLong(specification.MaxLength.Value));
                }
                break;

            case AttributeSpecificationType.Text when specification.MaxLength.HasValue:
            case AttributeSpecificationType.BbCode when specification.MaxLength.HasValue:
                if (specification.MaxLength.Value < trimmedValue.Length)
                {
                    return (false, AttributeValidationError.StringTooLong(specification.MaxLength.Value));
                }
                break;

            case AttributeSpecificationType.TextList:
            case AttributeSpecificationType.NumberList:
            case AttributeSpecificationType.TextNumberList:
                if (specification.Values != null && specification.Values.All(v => v.Value != trimmedValue))
                {
                    return (false,
                        AttributeValidationError.NotPresentInList(specification.Values.Select(v => v.Value)));
                }
                break;

            case AttributeSpecificationType.Text:
            case AttributeSpecificationType.BbCode:
                break;

            default:
                return (false, ValidationError.Invalid);
        }

        return (true, null);
    }
}
