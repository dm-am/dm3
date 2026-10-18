using System;

namespace DM.Web.API.Swagger;

/// <summary>
/// This action answers 201 and sets no <c>Location</c>.
/// </summary>
/// <remarks>
/// API_DESIGN lets a created thing with no address of its own answer 201 and
/// leave the header off: a Location on the collection would hand the client the
/// whole list instead of the record. The case is written down instead of being
/// left to the reader for one reason: the contract is generated, and a filter
/// that publishes "Location, required" over every 201 promises a header these
/// operations have never sent. A consumer written against that document follows
/// a header that is not there.
///
/// So the attribute is what the filter reads, and CreatedLocationShould keeps
/// the two halves from drifting: an action that calls CreatedAtRoute may not
/// carry it, and an action that answers 201 any other way has to. Removing the
/// attribute means giving the created thing an address, which is worth it only
/// when something besides the header needs that address.
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class CreatedWithoutLocationAttribute : Attribute
{
}
