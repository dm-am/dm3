using System;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.Games;
using DM.Testing;
using DM.Testing.Dsl;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Game.Tests.Authorization;

/// <summary>
/// Gates the attribute schemas. Use decides which schema a master may attach to
/// a new game, and the answer is silently swallowed by the caller: a wrong deny
/// here does not surface as a 403, it creates the game without the schema.
/// </summary>
public class AttributeSchemaIntentionResolverShould : UnitTestBase
{
    private readonly AttributeSchemaIntentionResolver resolver = new();

    private static readonly Guid AuthorId = Guid.NewGuid();
    private static readonly Guid StrangerId = Guid.NewGuid();

    private static AttributeSchema Schema(SchemaType type, Guid? authorId) =>
        new()
        {
            Id = Guid.NewGuid(),
            Type = type,
            Author = authorId.HasValue ? new GeneralUser { UserId = authorId.Value } : null
        };

    #region Use

    [Fact]
    public void OpenAPublicSchemaToAStranger()
    {
        var user = Create.User(StrangerId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, AttributeSchemaIntention.Use, Schema(SchemaType.Public, AuthorId))
            .Should().BeTrue();
    }

    [Fact]
    public void OpenAPublicSchemaToAnAnonymousCaller()
    {
        // Nothing about a public schema depends on who is asking, so the
        // resolver must answer without an identity rather than fail closed.
        resolver.IsAllowed(AuthenticatedUser.Guest, AttributeSchemaIntention.Use, Schema(SchemaType.Public, AuthorId))
            .Should().BeTrue();
    }

    [Fact]
    public void OpenAPublicSystemSchemaToEveryone()
    {
        var user = Create.User(StrangerId).WithRole(UserRole.RegularUser).Please();
        var schema = Schema(SchemaType.Public, null);

        // The seeded system schema is the one every game gets by default and it
        // has no author to match against.
        resolver.IsAllowed(user, AttributeSchemaIntention.Use, schema).Should().BeTrue();
        resolver.IsAllowed(AuthenticatedUser.Guest, AttributeSchemaIntention.Use, schema).Should().BeTrue();
    }

    [Fact]
    public void OpenAPrivateSchemaToItsAuthor()
    {
        var user = Create.User(AuthorId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, AttributeSchemaIntention.Use, Schema(SchemaType.Private, AuthorId))
            .Should().BeTrue();
    }

    [Fact]
    public void KeepAPrivateSchemaFromAStranger()
    {
        var user = Create.User(StrangerId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, AttributeSchemaIntention.Use, Schema(SchemaType.Private, AuthorId))
            .Should().BeFalse();
    }

    [Fact]
    public void KeepAPrivateSchemaFromSiteAdministration()
    {
        var user = Create.User(StrangerId).WithRole(UserRole.Admin).Please();

        // Authorship is the whole rule for a private schema. No site rank
        // borrows somebody else's attribute set into a game.
        resolver.IsAllowed(user, AttributeSchemaIntention.Use, Schema(SchemaType.Private, AuthorId))
            .Should().BeFalse();
    }

    [Fact]
    public void KeepAPrivateSchemaFromAnAnonymousCaller()
    {
        resolver.IsAllowed(AuthenticatedUser.Guest, AttributeSchemaIntention.Use, Schema(SchemaType.Private, AuthorId))
            .Should().BeFalse();
    }

    #endregion

    #region Edit and Delete

    [Theory]
    [InlineData(AttributeSchemaIntention.Edit)]
    [InlineData(AttributeSchemaIntention.Delete)]
    public void LetTheAuthorEditAndDeleteTheirOwnSchema(AttributeSchemaIntention intention)
    {
        var user = Create.User(AuthorId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, intention, Schema(SchemaType.Private, AuthorId)).Should().BeTrue();
    }

    [Theory]
    [InlineData(AttributeSchemaIntention.Edit)]
    [InlineData(AttributeSchemaIntention.Delete)]
    public void KeepAStrangerFromEditingAndDeletingSomebodyElsesSchema(AttributeSchemaIntention intention)
    {
        var user = Create.User(StrangerId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, intention, Schema(SchemaType.Private, AuthorId)).Should().BeFalse();
    }

    [Theory]
    [InlineData(AttributeSchemaIntention.Edit, SchemaType.Public)]
    [InlineData(AttributeSchemaIntention.Edit, SchemaType.Private)]
    [InlineData(AttributeSchemaIntention.Delete, SchemaType.Public)]
    [InlineData(AttributeSchemaIntention.Delete, SchemaType.Private)]
    public void LeaveASystemSchemaEditableByNobody(AttributeSchemaIntention intention, SchemaType type)
    {
        var user = Create.User(AuthorId).WithRole(UserRole.Admin).Please();

        // An authorless schema is compared through a lifted Guid?, so the null
        // author never equals anyone's id — including the guest, whose UserId is
        // the default Guid. Unlifting this comparison would hand every system
        // schema to the first anonymous request.
        resolver.IsAllowed(user, intention, Schema(type, null)).Should().BeFalse();
        resolver.IsAllowed(AuthenticatedUser.Guest, intention, Schema(type, null)).Should().BeFalse();
    }

    [Fact]
    public void LetTheAuthorEditTheirPublicSchema()
    {
        var user = Create.User(AuthorId).WithRole(UserRole.RegularUser).Please();

        // Behaviour as found, not a rule that was chosen: SchemaType documents
        // Public as "nobody can edit", yet Edit only ever asks about authorship.
        // Pinned so that reconciling the two is a deliberate change.
        resolver.IsAllowed(user, AttributeSchemaIntention.Edit, Schema(SchemaType.Public, AuthorId))
            .Should().BeTrue();
    }

    #endregion
}
