using System;

namespace DM.Web.API.Swagger;

/// <summary>
/// This action answers 201 and sets no <c>Location</c>.
/// </summary>
/// <remarks>
/// API_DESIGN says a created thing with no address of its own should not answer
/// 201 at all, so every one of these is a debt rather than a design. The debt is
/// written down instead of being left to the reader for one reason: the contract
/// is generated, and a filter that publishes "Location, required" over every 201
/// promises a header eighteen operations have never sent. A consumer written
/// against that document follows a header that is not there.
///
/// So the attribute is what the filter reads, and CreatedLocationShould keeps
/// the two halves from drifting: an action that calls CreatedAtRoute may not
/// carry it, and an action that answers 201 any other way has to. The list can
/// only shrink — removing the attribute means giving the response an address or
/// giving it a different status, and both are the fix.
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class CreatedWithoutLocationAttribute : Attribute
{
}
