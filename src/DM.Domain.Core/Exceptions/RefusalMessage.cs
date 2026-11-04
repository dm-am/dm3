using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Exceptions;

/// <summary>
/// Refusal texts that more than one throw site needs
/// </summary>
/// <remarks>
/// The message of an <see cref="HttpException"/> becomes the title of the problem
/// document, and the title is the sentence the client shows the reader. These are
/// therefore product copy in Russian, not developer strings: an English message
/// here is a Russian-speaking user reading English.
///
/// Texts used from more than one place live here, and so does a refusal whose
/// neighbours in the same file come from here: one literal among constants is the
/// sentence a search for its wording misses. Any other refusal stays at its call site.
///
/// One class, cut by module into partial files (RefusalMessage.Account, .Game and
/// the rest), so a text is found by the module that says it. This part keeps what
/// every module shares: the form refusal, the links from mail and the status machines.
/// </remarks>
public static partial class RefusalMessage
{
    // Fields

    /// <summary>
    /// Title of a refusal whose substance is the per-field errors beside it
    /// </summary>
    /// <remarks>
    /// The same sentence the middleware gives a validation failure: to the reader
    /// a rejected form is one event, whether the rules were checked by the
    /// validator or by hand in a controller.
    /// </remarks>
    public const string InvalidData = "Некорректные данные";

    // Request protection

    /// <summary>
    /// The browser sent a state-changing request from a page of another site
    /// </summary>
    public const string ForeignOrigin = "Запрос пришел с недопустимого адреса";

    /// <summary>
    /// Title of a rate-limited answer; the seconds to wait travel in Retry-After
    /// </summary>
    public const string TooManyRequests = "Слишком много запросов";

    /// <summary>
    /// The detail under <see cref="TooManyRequests" />, for a caller without a screen
    /// that spells the wait
    /// </summary>
    public const string RateLimitExceeded = "Лимит запросов превышен. Повторите позже.";

    // Links from mail and notifications

    /// <summary>
    /// The token behind the link was consumed, or never existed
    /// </summary>
    public const string LinkInvalidOrUsed = "Ссылка недействительна или уже использована";

    /// <summary>
    /// The approval token behind the link is unknown or past its deadline
    /// </summary>
    public const string LinkInvalidOrExpired = "Ссылка недействительна или устарела";

    // Status transitions

    /// <summary>
    /// The requested status is not reachable from the current one
    /// </summary>
    public const string UnknownStatusTransition = "Недопустимая смена статуса";

    /// <summary>
    /// The move is one of the defined ones, but not from the state the subject
    /// is in now. Said by the module status machine and by the character one,
    /// which are two machines over two vocabularies and one sentence.
    /// </summary>
    public static string IllegalStatusTransition(object transition, object currentStatus) =>
        $"Переход \"{transition}\" недоступен из статуса \"{currentStatus}\"";

    /// <summary>
    /// The same refusal for a module, which also names the reason it is closed
    /// for: Closed is the only status that stores one.
    /// </summary>
    public static string IllegalStatusTransition(
        object transition, ModuleStatus currentStatus, ClosedReason closedReason) =>
        IllegalStatusTransition(transition, currentStatus) +
        (currentStatus == ModuleStatus.Closed ? $" ({closedReason})" : "");

    /// <summary>
    /// The requested premoderation status is not reachable from the current one
    /// </summary>
    public const string UnknownPremoderationTransition = "Недопустимая смена статуса премодерации";

    /// <summary>
    /// The author may ask for a verdict only while the module is awaiting edits.
    /// The two moderation verdicts are legal from every status and never say this.
    /// </summary>
    public static string CannotSubmitForApproval(object currentStatus) =>
        $"Нельзя отправить на подтверждение из статуса \"{currentStatus}\"";
}
