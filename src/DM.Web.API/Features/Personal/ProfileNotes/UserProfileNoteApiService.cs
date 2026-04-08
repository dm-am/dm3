using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Personal.Features.ProfileNotes;
using DomainUserProfileNote = DM.Domain.Personal.Features.ProfileNotes.UserProfileNote;

namespace DM.Web.API.Features.Personal.ProfileNotes;

/// <inheritdoc />
internal class UserProfileNoteApiService : IUserProfileNoteApiService
{
    private readonly IUserProfileNoteService _userProfileNoteService;

    /// <inheritdoc />
    public UserProfileNoteApiService(IUserProfileNoteService userProfileNoteService)
    {
        _userProfileNoteService = userProfileNoteService;
    }

    /// <inheritdoc />
    public async Task<UserProfileNote?> GetNote(string username, CancellationToken ct = default)
    {
        var note = await _userProfileNoteService.GetNote(username, ct);
        if (note == null)
        {
            return null;
        }

        return MapToApiDto(note);
    }

    /// <inheritdoc />
    public async Task<UserProfileNote?> UpsertNote(string username, UserProfileNoteRequest request, CancellationToken ct = default)
    {
        var createNote = new CreateUserProfileNote
        {
            SubjectUsername = username,
            Text = request.Text
        };

        var note = await _userProfileNoteService.UpsertNote(createNote, ct);
        if (note == null)
        {
            return null;
        }
        return MapToApiDto(note);
    }

    /// <inheritdoc />
    public async Task DeleteNote(string username, CancellationToken ct = default)
    {
        await _userProfileNoteService.DeleteNote(username, ct);
    }

    private static UserProfileNote MapToApiDto(DomainUserProfileNote dto)
    {
        return new UserProfileNote
        {
            Id = dto.Id,
            Username = dto.SubjectUsername,
            Text = dto.Text,
            CreatedUtc = dto.CreatedUtc,
            UpdatedUtc = dto.UpdatedUtc
        };
    }
}
