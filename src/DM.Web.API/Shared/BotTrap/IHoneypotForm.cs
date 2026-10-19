namespace DM.Web.API.Shared.BotTrap;

/// <summary>
/// A form open to guests that carries a field people never see and scripts fill.
/// </summary>
/// <remarks>
/// <see cref="HoneypotFilter" /> refuses a request with the trap filled before the
/// action runs, so no form repeats the check.
/// </remarks>
public interface IHoneypotForm
{
    /// <summary>
    /// The trap: hidden from people, so a value in it is a script's.
    /// </summary>
    string? Website { get; }

    /// <summary>
    /// The field the refusal is filed under and its sentence. The field is one the
    /// form shows, so the sentence appears on screen; it is never the trap itself,
    /// which the form hides together with whatever is filed under it. The sentence
    /// says the form failed and not why, so a script learns nothing.
    /// </summary>
    (string Field, string Sentence) Refusal { get; }
}
