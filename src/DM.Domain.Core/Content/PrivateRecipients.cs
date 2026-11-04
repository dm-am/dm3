using System.Text;

namespace DM.Domain.Core.Content;

/// <summary>
/// The recipients line of what a game post keeps for some of its readers, the
/// words after "Получатели:" in the tooltip of a hidden block and of a marked file.
/// </summary>
/// <remarks>
/// The leads read every hidden block, so the line names them first, as DM2 posts
/// do; a name written for a lead is left out, matched with only A-Z folded, as a
/// tag name is. With no names the line is the leads alone.
/// </remarks>
public static class PrivateRecipients
{
    /// <summary>The leads, as the line names them, in its order.</summary>
    private static readonly string[] Leads = ["DungeonMaster", "Assistant"];

    /// <summary>The line naming the leads and then <paramref name="names"/>.</summary>
    /// <param name="names">The addressees, in the order to name them.</param>
    public static string Line(IEnumerable<string> names)
    {
        var line = new List<string>(Leads);
        foreach (var part in names)
        {
            var name = part.Trim();
            if (name.Length > 0 && !Leads.Any(lead => Ascii.EqualsIgnoreCase(name, lead))) line.Add(name);
        }

        return string.Join(", ", line);
    }
}
