using System.Collections.Generic;

namespace DM.Web.API.Shared.Dto;

/// <summary>
/// Enveloped DTO model
/// </summary>
/// <typeparam name="T">Enveloped type</typeparam>
/// <remarks>
/// There is no metadata slot. It stood here as <c>object?</c>, so it published
/// itself into all 42 Envelope schemas as a nullable property with no type at
/// all — a generated client got <c>metadata: any</c> on every single resource —
/// and not one of the 127 construction sites ever passed a second argument.
/// An extension point that describes nothing is worse than none: the consumer
/// can neither use it nor knowingly ignore it. It comes back when there is a
/// real case for it, with a type.
/// </remarks>
public class Envelope<T>
{
    /// <inheritdoc />
    public Envelope(T resource)
    {
        Resource = resource;
    }

    /// <summary>
    /// Enveloped resource
    /// </summary>
    public T Resource { get; }
}

/// <summary>
/// Enveloped list DTO model
/// </summary>
/// <typeparam name="T">Enveloped type</typeparam>
public class ListEnvelope<T>
{
    /// <inheritdoc />
    public ListEnvelope(IEnumerable<T> resources, PagingInfo? paging = null)
    {
        Resources = resources;
        Paging = paging;
    }

    /// <summary>
    /// Enveloped resources
    /// </summary>
    public IEnumerable<T> Resources { get; }

    /// <summary>
    /// Paging data
    /// </summary>
    public PagingInfo? Paging { get; }

    /// <summary>
    /// Deconstructor for tuple-style usage
    /// </summary>
    public void Deconstruct(out IEnumerable<T> resources, out PagingInfo? paging)
    {
        resources = Resources;
        paging = Paging;
    }
}
