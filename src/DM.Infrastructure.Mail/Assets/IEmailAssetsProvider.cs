namespace DM.Infrastructure.Mail.Assets;

/// <summary>
/// Provides embedded assets for email templates (internal implementation contract).
/// </summary>
/// <remarks>
/// DEPRECATED: Use <see cref="DM.Domain.Core.Mail.IEmailAssetsProvider"/> from Domain.Core.Mail instead.
/// This interface will be removed after migration is complete.
/// </remarks>
internal interface IEmailAssetsProvider : DM.Domain.Core.Mail.IEmailAssetsProvider;
