using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace DM.Web.API.Tests.Shared;

/// <summary>
/// A request body is its own type, never the response DTO of the same resource.
/// </summary>
/// <remarks>
/// A response DTO answers "what does this resource look like"; a request body
/// answers "what may the caller change". Using one for both makes the contract
/// ask for fields that do nothing — editing the text of a comment requires
/// assembling an author of 28 fields and an array of likers, none of which is
/// read — and leaves the server with nothing but a hand-written Ignore per field
/// standing between an input and the domain.
///
/// That defence has already failed once, and the comment recording it still sits
/// in CharacterMappingProfile: PATCH of a character without a privacy block
/// demoted an NPC to a player character, because a bool? destination took a
/// plain false from a missing source. The repair was one line; the class of
/// defect was untouched, and the next field added to a response DTO lands in the
/// update path by name again.
///
/// The list below is the whole of the debt, frozen. It may shrink and must not
/// grow: a new action binding a response DTO turns this red on the first build.
/// </remarks>
public class RequestBodyTypesShould
{
    /// <summary>
    /// Actions whose body type is also a response type, as inherited. Named by
    /// action rather than by type: the fix is a per-endpoint request DTO, so the
    /// list shrinks one action at a time.
    /// </summary>
    /// <remarks>
    /// Creation shares the debt with editing. A POST that binds the resource DTO
    /// asks the caller for the id, the author and the counters the server is about
    /// to compute, and the only thing keeping them out of the domain is the same
    /// per-field Ignore.
    /// </remarks>
    private static readonly string[] Legacy =
    [
        "AttributeSchemaController.PostSchema",
        "BlogCommentController.PatchBlogComment",
        "CharacterController.PostCharacter",
        "CharacterController.PutCharacter",
        "GameBlacklistController.PostBlacklist",
        "GameCommentController.PatchGameComment",
        "GameController.PatchGameDetails",
        "PostController.PatchPost",
        "PublicationCommentController.PatchPublicationComment",
        "RoomController.CreatePostPendency",
        "RoomController.PatchAccess",
        "RoomController.PatchRoom",
        "RoomController.PostAccess",
        "TopicCommentController.PatchTopicComment",
    ];

    /// <summary>Every controller action in the host.</summary>
    private static MethodInfo[] Actions() => typeof(Startup).Assembly
        .GetTypes()
        .Where(t => t.IsClass && !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t))
        .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any())
        .ToArray();

    /// <summary>Types the host declares as a success body anywhere.</summary>
    private static HashSet<Type> ResponseTypes()
    {
        var types = new HashSet<Type>();
        foreach (var action in Actions())
        {
            foreach (var declared in action.GetCustomAttributes<ProducesResponseTypeAttribute>())
            {
                if (declared.StatusCode >= 400 || declared.Type == null || declared.Type == typeof(void))
                {
                    continue;
                }

                // Envelope<T> and its kin are response shapes by construction;
                // what matters is the resource inside them.
                types.Add(declared.Type);
                if (declared.Type.IsGenericType)
                {
                    foreach (var argument in declared.Type.GetGenericArguments())
                    {
                        types.Add(argument);
                    }
                }
            }
        }

        return types;
    }

    [Fact]
    public void NotBeAResponseType()
    {
        var responses = ResponseTypes();
        responses.Should().NotBeEmpty("the host declares success bodies");

        var offenders = Actions()
            .Where(action => action.GetParameters()
                .Any(p => p.GetCustomAttribute<FromBodyAttribute>() != null && responses.Contains(p.ParameterType)))
            .Select(action => $"{action.DeclaringType!.Name}.{action.Name}")
            .Where(name => !Legacy.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        offenders.Should().BeEmpty(
            "a request body names what the caller may change, see the class remarks");
    }

    [Fact]
    public void LeaveNoStaleNameOnTheLegacyList()
    {
        var responses = ResponseTypes();

        var offending = Actions()
            .Where(action => action.GetParameters()
                .Any(p => p.GetCustomAttribute<FromBodyAttribute>() != null && responses.Contains(p.ParameterType)))
            .Select(action => $"{action.DeclaringType!.Name}.{action.Name}")
            .ToArray();

        Legacy.Should().BeSubsetOf(offending,
            "an entry that outlives the action it exempts is an exemption nobody " +
            "reviewed, waiting for a new action to be given the same name");
    }
}
