using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Configuration;
using DM.Domain.Core.Abstractions;

namespace DM.Domain.Account.Features.UsernameChange;

/// <inheritdoc />
internal class UsernameChangeExpiryProcessor : IUsernameChangeExpiryProcessor
{
    /// <summary>What the requester is told when no moderator got to their request.</summary>
    private const string UnreviewedComment = "Автоматически отклонено: истек срок ожидания модерации";

    /// <summary>Why the approval lapsed. Approving takes no comment, so this often stands alone.</summary>
    private const string ApprovalExpiredComment =
        "Токен истек: пользователь не выбрал новое имя в отведенное время";

    private readonly IUsernameChangeRepository _repository;
    private readonly IDateTimeProvider _dateTimeProvider;

    public UsernameChangeExpiryProcessor(
        IUsernameChangeRepository repository,
        IDateTimeProvider dateTimeProvider)
    {
        _repository = repository;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public Task<int> ExpireUnreviewedAsync(CancellationToken cancellationToken = default)
    {
        var now = _dateTimeProvider.Now;
        return _repository.ExpireUnreviewedRequests(
            now - AccountRetentionPolicy.UsernameChangeReviewWindow, now, UnreviewedComment, cancellationToken);
    }

    /// <inheritdoc />
    public Task<int> ExpireApprovalTokensAsync(CancellationToken cancellationToken = default) =>
        _repository.ExpireApprovalTokens(_dateTimeProvider.Now, ApprovalExpiredComment, cancellationToken);
}
