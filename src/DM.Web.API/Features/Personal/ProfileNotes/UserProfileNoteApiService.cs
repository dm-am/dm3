using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Personal.Features.ProfileNotes;
using DomainUserProfileNote = DM.Domain.Personal.Features.ProfileNotes.UserProfileNote;

namespace DM.Web.API.Features.Personal.ProfileNotes;

public class UserProfileNoteApiService
{
    private readonly IUserProfileNoteService _userProfileNoteService;

    public UserProfileNoteApiService(IUserProfileNoteService userProfileNoteService)
    {
        _userProfileNoteService = userProfileNoteService;
    }

    public async Task<UserProfileNote?> GetNote(string username, CancellationToken ct = default)
    {
        var note = await _userProfileNoteService.GetNote(username, ct);
        if (note == null)
        {
            return null;
        }

        return MapToApiDto(note);
    }

    /// <summary>Empty or whitespace text deletes the note and answers null.</summary>
    public async Task<UserProfileNote?> UpsertNote(string username, UserProfileNoteRequest request, CancellationToken ct = default)
    {
        var note = await _userProfileNoteService.UpsertNote(
            new CreateUserProfileNote { SubjectUsername = username, Text = request.Text }, ct);
        return note == null ? null : MapToApiDto(note);
    }

    public Task DeleteNote(string username, CancellationToken ct = default) =>
        _userProfileNoteService.DeleteNote(username, ct);

    private static UserProfileNote MapToApiDto(DomainUserProfileNote dto)
    {
        return new UserProfileNote
        {
            Id = dto.Id,
            Username = dto.SubjectUsername,
            Text = dto.Text,
            CreatedUtc = dto.CreatedUtc,
            ModifiedUtc = dto.ModifiedUtc
        };
    }
}
