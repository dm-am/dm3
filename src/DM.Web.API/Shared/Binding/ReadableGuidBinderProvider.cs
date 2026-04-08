using System;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DM.Web.API.Shared.Binding;

/// <summary>
/// Model binder provider for ReadableGuid format (Base64 URL-safe encoded GUIDs).
/// Handles scalar Guid and Guid? types in route and query parameters.
/// </summary>
internal class ReadableGuidBinderProvider : IModelBinderProvider
{
    /// <inheritdoc />
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        var modelType = context.Metadata.ModelType;

        // Only handle scalar Guid types (route parameters, single query params)
        // Guid collections in request bodies are handled by JSON deserialization
        if (modelType == typeof(Guid) || modelType == typeof(Guid?))
        {
            return new ReadableGuidBinder();
        }

        return null;
    }
}