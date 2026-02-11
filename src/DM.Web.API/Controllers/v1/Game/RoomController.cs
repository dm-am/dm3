using System;
using System.Threading.Tasks;
using DM.Services.Core.Dto.Enums;
using DM.Web.API.Authentication;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Games;
using DM.Web.API.Services.Game.Rooms;
using DM.Web.API.Services.Game;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Controllers.v1.Game;

/// <inheritdoc />
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

    /// <inheritdoc />
    public RoomController(
        IRoomApiService roomApiService,
        IRoomAccessApiService accessApiService,
        IPostPendencyApiService postPendencyApiService,
        IPostApiService postApiService)
    {
        _roomApiService = roomApiService;
        _accessApiService = accessApiService;
        _postPendencyApiService = postPendencyApiService;
        _postApiService = postApiService;
    }

    /// <summary>
    /// Get list of rooms in game
    /// </summary>
    /// <param name="id">Game identifier</param>
    /// <response code="200">Returns the list of rooms in the game</response>
    /// <response code="410">Game not found</response>
    [HttpGet("~/v1/games/{id}/rooms", Name = nameof(GetRooms))]
    [ProducesResponseType(typeof(ListEnvelope<Room>), 200)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> GetRooms(Guid id) => Ok(await _roomApiService.GetAll(id));

    /// <summary>
    /// Get list of chat rooms in game
    /// </summary>
    /// <remarks>
    /// Returns only rooms with type=Chat. These rooms are used for out-of-character
    /// communication between players.
    /// </remarks>
    /// <param name="id">Game identifier</param>
    /// <response code="200">List of chat rooms</response>
    /// <response code="410">Game not found</response>
    [HttpGet("~/v1/games/{id}/chat-rooms", Name = nameof(GetChatRooms))]
    [ProducesResponseType(typeof(ListEnvelope<Room>), 200)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> GetChatRooms(Guid id) =>
        Ok(await _roomApiService.GetByType(id, RoomType.Chat));

    /// <summary>
    /// Create new room in game
    /// </summary>
    /// <param name="id">Game identifier</param>
    /// <param name="room">Room details</param>
    /// <response code="201">Resource created successfully</response>
    /// <response code="400">Some of room properties were invalid</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to create a room in this game</response>
    /// <response code="410">Game not found</response>
    [HttpPost("~/v1/games/{id}/rooms", Name = nameof(PostRoom))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Room>), 201)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> PostRoom(Guid id, [FromBody] CreateRoomRequest room)
    {
        var result = await _roomApiService.Create(id, room);
        return CreatedAtRoute(nameof(GetRoom),
            new {id = result.Resource.Id}, result);
    }

    /// <summary>
    /// Get room
    /// </summary>
    /// <param name="id">Room identifier</param>
    /// <response code="200">Returns the room details</response>
    /// <response code="410">Room not found</response>
    [HttpGet("{id}", Name = nameof(GetRoom))]
    [ProducesResponseType(typeof(Envelope<Room>), 200)]
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
    /// <response code="410">Room not found</response>
    [HttpPatch("{id}", Name = nameof(PatchRoom))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Room>), 200)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> PatchRoom(Guid id, [FromBody] Room room) =>
        Ok(await _roomApiService.Update(id, room));

    /// <summary>
    /// Delete room
    /// </summary>
    /// <param name="id">Room identifier</param>
    /// <response code="204">Operation completed successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to remove the room</response>
    /// <response code="410">Room not found</response>
    [HttpDelete("{id}", Name = nameof(DeleteRoom))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
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
    /// <response code="410">Room not found</response>
    [HttpPost("{id}/accesses", Name = nameof(PostAccess))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<RoomAccess>), 201)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 409)]
    [ProducesResponseType(typeof(GeneralError), 404)]
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
    /// <response code="410">Access not found</response>
    [HttpPatch("accesses/{id}", Name = nameof(PatchAccess))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<RoomAccess>), 200)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> PatchAccess(Guid id, [FromBody] RoomAccess access) =>
        Ok(await _accessApiService.Update(id, access));

    /// <summary>
    /// Delete access for room
    /// </summary>
    /// <param name="id">Room access identifier</param>
    /// <response code="204">Operation completed successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to delete this access</response>
    /// <response code="410">Access not found</response>
    [HttpDelete("accesses/{id}", Name = nameof(DeleteAccess))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
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
    /// <response code="409">Post pendency already exists</response>
    /// <response code="410">Room not found</response>
    [HttpPost("{id}/pendencies", Name = nameof(CreatePostPendency))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<PostPendency>), 201)]
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
    /// <response code="410">Post pendency not found</response>
    [HttpDelete("pendencies/{id}", Name = nameof(DeletePostPendency))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
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
    /// <response code="410">Room not found</response>
    [HttpDelete("{id}/posts/unread", Name = nameof(MarkPostsAsRead))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> MarkPostsAsRead(Guid id)
    {
        await _postApiService.MarkAsRead(id);
        return NoContent();
    }
}