using System.Collections.Generic;

namespace DM.Domain.Account.Features.TwoFactor;

/// <summary>
/// The values that let a person in when the device is gone.
/// </summary>
public interface IRecoveryCodeFactory
{
    /// <summary>
    /// A fresh set of codes, in the clear.
    /// </summary>
    /// <remarks>
    /// The only moment these exist as values: the caller shows them once and
    /// stores nothing but their hashes.
    /// </remarks>
    /// <param name="count">How many codes the set holds</param>
    IReadOnlyList<string> Create(int count);

    /// <summary>
    /// The form a code is compared and stored in.
    /// </summary>
    /// <remarks>
    /// Case and separators are dropped, and the letters Crockford's alphabet
    /// leaves out are folded onto the digits they are mistaken for. A code
    /// copied out by hand onto paper and typed back with dashes and a lowercase
    /// l has to be the same code.
    /// </remarks>
    /// <param name="code">Value as the person typed it</param>
    string Normalize(string code);

    /// <summary>
    /// What the database keeps for a code.
    /// </summary>
    /// <param name="code">Value as the person typed it</param>
    byte[] Hash(string code);
}
