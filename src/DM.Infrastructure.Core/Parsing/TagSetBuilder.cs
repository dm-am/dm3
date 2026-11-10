using System.Collections.Generic;
using BBCodeParser.Tags;

namespace DM.Infrastructure.Core.Parsing;

internal class TagSetBuilder
{
    private readonly List<Tag> _set;

    public TagSetBuilder(IEnumerable<Tag> defaultSet)
    {
        _set = new List<Tag>(defaultSet);
    }

    public Tag[] Build()
    {
        return _set.ToArray();
    }

    public TagSetBuilder With(params Tag[] tags)
    {
        _set.AddRange(tags);
        return this;
    }
}
