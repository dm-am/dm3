using System.Threading;
using System.Threading.Tasks;
using DM.Services.Community.BusinessProcesses.ProfileNotes;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Users;

namespace DM.Web.API.Services.Users;

/// <inheritdoc />
internal class ProfileNoteApiService : IProfileNoteApiService
{
    private readonly IProfileNoteService _profileNoteService;

    /// <inheritdoc />
    public ProfileNoteApiService(IProfileNoteService profileNoteService)
    {
        _profileNoteService = profileNoteService;
    }

    /// <inheritdoc />
    public async Task<Envelope<ProfileNote>?> GetNote(string login, CancellationToken ct = default)
    {
        var note = await _profileNoteService.GetNote(login, ct);
        if (note == null)
        {
            return null;
        }

        return new Envelope<ProfileNote>(MapToApiDto(note));
    }

    /// <inheritdoc />
    public async Task<Envelope<ProfileNote>> UpsertNote(string login, ProfileNoteRequest request, CancellationToken ct = default)
    {
        var createNote = new CreateProfileNote
        {
            SubjectUserLogin = login,
            Text = request.Text
        };

        var note = await _profileNoteService.UpsertNote(createNote, ct);
        return new Envelope<ProfileNote>(MapToApiDto(note));
    }

    /// <inheritdoc />
    public async Task DeleteNote(string login, CancellationToken ct = default)
    {
        await _profileNoteService.DeleteNote(login, ct);
    }

    private static ProfileNote MapToApiDto(ProfileNoteDto dto)
    {
        return new ProfileNote
        {
            Id = dto.NoteId,
            UserLogin = dto.SubjectUserLogin,
            Text = dto.Text,
            CreatedAt = dto.CreatedUtc,
            UpdatedAt = dto.UpdatedUtc
        };
    }
}
