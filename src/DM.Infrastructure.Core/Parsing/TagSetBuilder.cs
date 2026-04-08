using System.Collections.Generic;
using BBCodeParser.Tags;

namespace DM.Infrastructure.Core.Parsing;

/// <summary>
/// BBCode parser tag set
/// </summary>
internal class TagSetBuilder
{
    private readonly List<Tag> _set;

    /// <inheritdoc />
    public TagSetBuilder(IEnumerable<Tag> defaultSet)
    {
        _set = new List<Tag>(defaultSet);
    }

    /// <summary>
    /// Build set of tags
    /// </summary>
    /// <returns>Array of tags</returns>
    public Tag[] Build()
    {
        return _set.ToArray();
    }

    /// <summary>
    /// Add tags to set
    /// </summary>
    /// <param name="tags">Tags</param>
    /// <returns>Self</returns>
    public TagSetBuilder With(params Tag[] tags)
    {
        _set.AddRange(tags);
        return this;
    }

    /// <summary>
    /// Remove tags from set
    /// </summary>
    /// <param name="tags">Tags</param>
    /// <returns>Self</returns>
    public TagSetBuilder Without(params Tag[] tags)
    {
        foreach (var tag in tags)
        {
            _set.Remove(tag);
        }
        return this;
    }
}