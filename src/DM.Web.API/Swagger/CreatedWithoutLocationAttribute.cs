using System;

namespace DM.Web.API.Swagger;

/// <summary>
/// This action answers 201 and sets no <c>Location</c>.
/// </summary>
/// <remarks>
/// API_DESIGN lets a created thing with no address of its own leave the header
/// off, and the generated contract must not promise one for it.
/// CreatedLocationShould keeps the marker on exactly the actions that answer 201
/// without CreatedAtRoute.
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class CreatedWithoutLocationAttribute : Attribute
{
}
