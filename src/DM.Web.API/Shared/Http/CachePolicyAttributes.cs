using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Shared.Http;

/// <summary>
/// A response no cache may store.
/// </summary>
/// <remarks>
/// Per-caller data with no validator anywhere in this API: a stored copy would be
/// served without the origin being asked again, after a sign-out included.
/// </remarks>
internal sealed class PrivateResponseAttribute : ResponseCacheAttribute
{
    public PrivateResponseAttribute()
    {
        NoStore = true;
        Location = ResponseCacheLocation.None;
    }
}

/// <summary>
/// A catalogue: read without an identity, so every caller gets the same bytes,
/// and shared caches keep it for five minutes.
/// </summary>
internal sealed class CatalogueCacheAttribute : ResponseCacheAttribute
{
    public CatalogueCacheAttribute()
    {
        Duration = 300;
        Location = ResponseCacheLocation.Any;
    }
}
