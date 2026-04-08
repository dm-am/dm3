using System;
using System.Threading.Tasks;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Game.Games;
using DM.Web.API.Features.Game.Posts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Game.Rooms;

/// <summary>
/// Room management endpoints
/// </summary>
/// <remarks>
/// Provides CRUD operations for game rooms (locations/scenes).
/// Rooms contain posts, support access control, and post pendencies for turn-based gameplay.
/// </remarks>
[ApiController]
[Route("v1/rooms")]
[ApiExplorerSettings(GroupName = "Game")]
[Tags("Rooms")]
public class RoomController : ControllerBase
{
    private readonly IRoomApiService _roomApiService;
    private readonly IRoomAccessApiService _accessApiService;
    private readonly IPostPendencyApiService _postPendencyApiService;
    private readonly IPostApiService _postApiService;
    private readonly IGameApiService _gameApiService;

    /// <summary>
    /// Creates a new instance of RoomController
    /// </summary>
    public RoomController(
        IRoomApiService roomApiService,
        IRoomAccessApiService accessApiService,
        IPostPendencyApiService postPendencyApiService,
        IPostApiService postApiService,
        IGameApiService gameApiService)
    {
        _roomApiService = roomApiService;
        _accessApiService = accessApiService;
        _postPendencyApiService = postPendencyApiService;
        _postApiService = postApiService;
        _gameApiService = gameApiService;
    }

    private async Task<Guid> ResolveGameId(string id) =>
        Guid.TryParse(id, out var guid) ? guid : (await _gameApiService.GetByPublicId(id)).Resource.Id;

    /// <summary>
    /// Get list of rooms in game
    /// </summary>
    /// <param name="id">Game public ID (5 letters) or GUID</param>
    /// <response code="200">Returns the list of rooms in the game</response>
    /// <response code="404">Game not found</response>
    [HttpGet("~/v1/games/{id}/rooms", Name = nameof(GetRooms))]
    [ProducesResponseType(typeof(ListEnvelope<Room>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRooms(string id)
    {
        var gameId = await ResolveGameId(id);
        return Ok(await _roomApiService.GetAll(gameId));
    }

    /// <summary>
    /// Create new room in game
    /// </summary>
    /// <param name="id">Game public ID (5 letters) or GUID</param>
    /// <param name="room">Room details</param>
    /// <response code="201">Resource created successfully</response>
    /// <response code="400">Some of room properties were invalid</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to create a room in this game</response>
    /// <response code="404">Game not found</response>
    [HttpPost("~/v1/games/{id}/rooms", Name = nameof(PostRoom))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Room>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PostRoom(string id, [FromBody] CreateRoomRequest room)
    {
        var gameId = await ResolveGameId(id);
        var result = await _roomApiService.Create(gameId, room);
        return CreatedAtRoute(nameof(GetRoom),
            new {id = result.Resource.Id}, result);
    }

    /// <summary>
    /// Get room
    /// </summary>
    /// <param name="id">Room identifier</param>
    /// <response code="200">Returns the room details</response>
    /// <response code="404">Room not found</response>
    [HttpGet("{id}", Name = nameof(GetRoom))]
    [ProducesResponseType(typeof(Envelope<Room>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRoom(Guid id) => Ok(await _roomApiService.Get(id));

    /// <summary>
    /// Update room
    /// </summary>
    /// <param name="id">Room identifier</param>
    /// <param name="room">Updated room details</param>
    /// <response code="200">Returns the updated room</response>
    /// <response code="400">Some of room changed properties were invalid or passed id was not recognized</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to change some properties of this room</response>
    /// <response code="404">Room not found</response>
    [HttpPatch("{id}", Name = nameof(PatchRoom))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Room>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PatchRoom(Guid id, [FromBody] Room room) =>
        Ok(await _roomApiService.Update(id, room));

    /// <summary>
    /// Delete room
    /// </summary>
    /// <param name="id">Room identifier</param>
    /// <response code="204">Operation completed successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to remove the room</response>
    /// <response code="404">Room not found</response>
    [HttpDelete("{id}", Name = nameof(DeleteRoom))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteRoom(Guid id)
    {
        await _roomApiService.Delete(id);
        return NoContent();
    }

    /// <summary>
    /// Create new access for room
    /// </summary>
    /// <param name="id">Room identifier</param>
    /// <param name="access">Access details</param>
    /// <response code="201">Resource created successfully</response>
    /// <response code="400">Some of access parameters were invalid</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to create accesses in this room</response>
    /// <response code="409">Access already exists</response>
    /// <response code="404">Room not found</response>
    [HttpPost("{id}/accesses", Name = nameof(PostAccess))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<RoomAccess>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PostAccess(Guid id, [FromBody] RoomAccess access)
    {
        var result = await _accessApiService.Create(id, access);
        return CreatedAtRoute(nameof(GetRoom),
            new {id}, result);
    }

    /// <summary>
    /// Update access for room
    /// </summary>
    /// <param name="id">Room access identifier</param>
    /// <param name="access">Updated access details</param>
    /// <response code="200">Returns the updated room access</response>
    /// <response code="400">Some of access parameters were invalid</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to update this access</response>
    /// <response code="404">Access not found</response>
    [HttpPatch("accesses/{id}", Name = nameof(PatchAccess))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<RoomAccess>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PatchAccess(Guid id, [FromBody] RoomAccess access) =>
        Ok(await _accessApiService.Update(id, access));

    /// <summary>
    /// Delete access for room
    /// </summary>
    /// <param name="id">Room access identifier</param>
    /// <response code="204">Operation completed successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to delete this access</response>
    /// <response code="404">Access not found</response>
    [HttpDelete("accesses/{id}", Name = nameof(DeleteAccess))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAccess(Guid id)
    {
        await _accessApiService.Delete(id);
        return NoContent();
    }

    /// <summary>
    /// Create new post pendency
    /// </summary>
    /// <param name="id">Room identifier</param>
    /// <param name="postPendency">Post pendency details</param>
    /// <response code="201">Resource created successfully</response>
    /// <response code="400">Some of claim parameters were invalid</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to create post pendencies in this room</response>
    /// <response code="404">Room not found</response>
    /// <response code="409">Post pendency already exists</response>
    [HttpPost("{id}/pendencies", Name = nameof(CreatePostPendency))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<PostPendency>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreatePostPendency(Guid id, [FromBody] PostPendency postPendency)
    {
        var result = await _postPendencyApiService.Create(id, postPendency);
        return CreatedAtRoute(nameof(GetRoom), new {id}, result);
    }

    /// <summary>
    /// Delete post pendency
    /// </summary>
    /// <param name="id">Post pendency identifier</param>
    /// <response code="204">Operation completed successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to delete this post pendency</response>
    /// <response code="404">Post pendency not found</response>
    [HttpDelete("pendencies/{id}", Name = nameof(DeletePostPendency))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePostPendency(Guid id)
    {
        await _postPendencyApiService.Delete(id);
        return NoContent();
    }

    /// <summary>
    /// Mark all posts in room as read
    /// </summary>
    /// <param name="id">Room identifier</param>
    /// <response code="204">Operation completed successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="404">Room not found</response>
    [HttpDelete("{id}/posts/unread", Name = nameof(MarkPostsAsRead))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkPostsAsRead(Guid id)
    {
        await _postApiService.MarkAsRead(id);
        return NoContent();
    }
}
