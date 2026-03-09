namespace DM.Web.API.Shared.Dto;

/// <summary>
/// General error DTO model
/// </summary>
public class GeneralError
{
    /// <inheritdoc />
    public GeneralError(string message)
    {
        Message = message;
    }
        
    /// <summary>
    /// Client message
    /// </summary>
    public string Message { get; }
}