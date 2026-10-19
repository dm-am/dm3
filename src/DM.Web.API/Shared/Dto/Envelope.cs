using System.Collections.Generic;

namespace DM.Web.API.Shared.Dto;

/// <summary>
/// Enveloped DTO model
/// </summary>
/// <typeparam name="T">Enveloped type</typeparam>
public class Envelope<T>
{
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
}
