using System;
using System.Threading.Tasks;
using DM.Web.API.Authentication;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Games;
using DM.Web.API.Services.Gaming.Rooms;
using DM.Web.API.Services.Gaming;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Controllers.v1.Gaming;

/// <inheritdoc />
[ApiController]
[Route("v1")]
[ApiExplorerSettings(GroupName = "Game")]
public class RoomController : ControllerBase
{
    private readonly IRoomApiService roomApiService;
    private readonly IRoomClaimApiService claimApiService;
    private readonly IPendingPostApiService pendingPostApiService;
    private readonly IPostApiService postApiService;

    /// <inheritdoc />
    public RoomController(
        IRoomApiService roomApiService,
        IRoomClaimApiService claimApiService,
        IPendingPostApiService pendingPostApiService,
        IPostApiService postApiService)
    {
        this.roomApiService = roomApiService;
        this.claimApiService = claimApiService;
        this.pendingPostApiService = pendingPostApiService;
        this.postApiService = postApiService;
    }

    /// <summary>
    /// Get list of rooms in game
    /// </summary>
    /// <param name="id"></param>
    /// <response code="200"></response>
    /// <response code="410">Game not found</response>
    [HttpGet("games/{id}/rooms", Name = nameof(GetRooms))]
    [ProducesResponseType(typeof(ListEnvelope<Room>), 200)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    public async Task<IActionResult> GetRooms(Guid id) => Ok(await roomApiService.GetAll(id));

    /// <summary>
    /// Create new room in game
    /// </summary>
    /// <param name="id"></param>
    /// <param name="room"></param>
    /// <response code="201"></response>
    /// <response code="400">Some of room properties were invalid</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to create a room in this game</response>
    /// <response code="410">Game not found</response>
    [HttpPost("games/{id}/rooms", Name = nameof(PostRoom))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Room>), 201)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    public async Task<IActionResult> PostRoom(Guid id, [FromBody] Room room)
    {
        var result = await roomApiService.Create(id, room);
        return CreatedAtRoute(nameof(GetRoom),
            new {id = result.Resource.Id}, result);
    }

    /// <summary>
    /// Get room
    /// </summary>
    /// <param name="id"></param>
    /// <response code="200"></response>
    /// <response code="410">Room not found</response>
    [HttpGet("rooms/{id}", Name = nameof(GetRoom))]
    [ProducesResponseType(typeof(Envelope<Room>), 200)]
    public async Task<IActionResult> GetRoom(Guid id) => Ok(await roomApiService.Get(id));

    /// <summary>
    /// Update room
    /// </summary>
    /// <param name="id"></param>
    /// <param name="room"></param>
    /// <response code="200"></response>
    /// <response code="400">Some of room changed properties were invalid or passed id was not recognized</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not authorized to change some properties of this room</response>
    /// <response code="410">Room not found</response>
    [HttpPatch("rooms/{id}", Name = nameof(PatchRoom))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Room>), 200)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    public async Task<IActionResult> PatchRoom(Guid id, [FromBody] Room room) =>
        Ok(await roomApiService.Update(id, room));

    /// <summary>
    /// Delete room
    /// </summary>
    /// <param name="id"></param>
    /// <response code="204"></response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to remove the room</response>
    /// <response code="410">Room not found</response>
    [HttpDelete("rooms/{id}", Name = nameof(DeleteRoom))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    public async Task<IActionResult> DeleteRoom(Guid id)
    {
        await roomApiService.Delete(id);
        return NoContent();
    }

    /// <summary>
    /// Create new claim for room
    /// </summary>
    /// <param name="id"></param>
    /// <param name="claim"></param>
    /// <response code="201"></response>
    /// <response code="400">Some of claim parameters were invalid</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to create claims in this room</response>
    /// <response code="409">Claim already exists</response>
    /// <response code="410">Room not found</response>
    [HttpPost("rooms/{id}/claims", Name = nameof(PostClaim))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<RoomClaim>), 201)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 409)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    public async Task<IActionResult> PostClaim(Guid id, [FromBody] RoomClaim claim)
    {
        var result = await claimApiService.Create(id, claim);
        return CreatedAtRoute(nameof(GetRoom),
            new {id}, result);
    }

    /// <summary>
    /// Update claim for room
    /// </summary>
    /// <param name="id"></param>
    /// <param name="claim"></param>
    /// <response code="200"></response>
    /// <response code="400">Some of claim parameters were invalid</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to update this claim</response>
    /// <response code="410">Claim not found</response>
    [HttpPatch("rooms/claims/{id}", Name = nameof(PatchClaim))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<RoomClaim>), 200)]
    [ProducesResponseType(typeof(BadRequestError), 400)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    public async Task<IActionResult> PatchClaim(Guid id, [FromBody] RoomClaim claim) =>
        Ok(await claimApiService.Update(id, claim));

    /// <summary>
    /// Delete claim for room
    /// </summary>
    /// <param name="id"></param>
    /// <response code="204"></response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to delete this claim</response>
    /// <response code="410">Claim not found</response>
    [HttpDelete("rooms/claims/{id}", Name = nameof(DeleteClaim))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    public async Task<IActionResult> DeleteClaim(Guid id)
    {
        await claimApiService.Delete(id);
        return NoContent();
    }

    /// <summary>
    /// Create new post pendency
    /// </summary>
    /// <param name="id"></param>
    /// <param name="pendingPost"></param>
    /// <response code="201"></response>
    /// <response code="400">Some of claim parameters were invalid</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to create post pendings in this room</response>
    /// <response code="409">Post pending already exists</response>
    /// <response code="410">Room not found</response>
    [HttpPost("rooms/{id}/pendencies", Name = nameof(CreatePendingPost))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<PendingPost>), 201)]
    public async Task<IActionResult> CreatePendingPost(Guid id, [FromBody] PendingPost pendingPost)
    {
        var result = await pendingPostApiService.Create(id, pendingPost);
        return CreatedAtRoute(nameof(GetRoom), new {id}, result);
    }

    /// <summary>
    /// Delete post pendency
    /// </summary>
    /// <param name="id"></param>
    /// <response code="204"></response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">User is not allowed to delete this pending post</response>
    /// <response code="410">Pending post not found</response>
    [HttpDelete("rooms/pendencies/{id}", Name = nameof(DeletePendingPost))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    public async Task<IActionResult> DeletePendingPost(Guid id)
    {
        await pendingPostApiService.Delete(id);
        return NoContent();
    }

    /// <summary>
    /// Mark all posts in room as read
    /// </summary>
    /// <param name="id"></param>
    /// <response code="204"></response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="410">Room not found</response>
    [HttpDelete("rooms/{id}/posts/unread", Name = nameof(MarkPostsAsRead))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 410)]
    public async Task<IActionResult> MarkPostsAsRead(Guid id)
    {
        await postApiService.MarkAsRead(id);
        return NoContent();
    }
}