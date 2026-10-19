using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using AwesomeAssertions;
using DM.Web.API.Features.Personal.Notepads;
using Xunit;

namespace DM.Web.API.Tests.Features.Personal;

/// <summary>
/// The title of a notepad entry is refused past the length of its column, on both paths; checked as MVC
/// checks the request.
/// </summary>
public class NotepadEntryRequestShould
{
    [Fact]
    public void TakeACreatedTitleUpToTheColumn() =>
        Refusals(new CreateNotepadEntryRequest { Title = new string('a', 200), Content = "" }).Should().BeEmpty();

    [Fact]
    public void RefuseACreatedTitlePastTheColumn() =>
        Refusals(new CreateNotepadEntryRequest { Title = new string('a', 201), Content = "" })
            .Should().ContainSingle(refusal => refusal.MemberNames.Contains(nameof(CreateNotepadEntryRequest.Title)));

    [Fact]
    public void TakeAnEditedTitleUpToTheColumn() =>
        Refusals(new UpdateNotepadEntryRequest { Title = new string('a', 200) }).Should().BeEmpty();

    [Fact]
    public void RefuseAnEditedTitlePastTheColumn() =>
        Refusals(new UpdateNotepadEntryRequest { Title = new string('a', 201) })
            .Should().ContainSingle(refusal => refusal.MemberNames.Contains(nameof(UpdateNotepadEntryRequest.Title)));

    [Fact]
    public void LeaveAnOmittedTitleAlone() =>
        Refusals(new UpdateNotepadEntryRequest { Content = "text" }).Should().BeEmpty();

    private static List<ValidationResult> Refusals(object request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        return results;
    }
}
