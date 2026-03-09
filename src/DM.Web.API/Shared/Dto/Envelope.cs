using System.Collections.Generic;

namespace DM.Web.API.Shared.Dto;

/// <summary>
/// Enveloped DTO model
/// </summary>
/// <typeparam name="T">Enveloped type</typeparam>
public class Envelope<T>
{
    /// <inheritdoc />
    public Envelope(T resource, object? metadata = null)
    {
        Resource = resource;
        Metadata = metadata;
    }

    /// <summary>
    /// Enveloped resource
    /// </summary>
    public T Resource { get; }

    /// <summary>
    /// Additional metadata
    /// </summary>
    public object? Metadata { get; }
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