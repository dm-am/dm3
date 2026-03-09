using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using DM.Infrastructure.Core.Parsing;
using Microsoft.AspNetCore.Http;

namespace DM.Web.API.Shared.BbRendering;

/// <inheritdoc />
internal class BbConverterFactory : JsonConverterFactory
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IBbParserProvider _bbParserProvider;

    /// <inheritdoc />
    public BbConverterFactory(
        IHttpContextAccessor httpContextAccessor,
        IBbParserProvider bbParserProvider)
    {
        _httpContextAccessor = httpContextAccessor;
        _bbParserProvider = bbParserProvider;
    }

    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsSubclassOf(typeof(BbText));

    /// <inheritdoc />
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converter = (JsonConverter) Activator.CreateInstance(
            typeof(BbConverter<>).MakeGenericType(typeToConvert),
            BindingFlags.Instance | BindingFlags.Public,
            null, new object[] {_httpContextAccessor, _bbParserProvider}, null)!;
        return converter;
    }

    private class BbConverter<TBbText>(
        IHttpContextAccessor httpContextAccessor,
        IBbParserProvider bbParserProvider)
        : JsonConverter<TBbText>
        where TBbText : BbText, new()
    {
        public override TBbText Read(ref Utf8JsonReader reader, Type typeToConvert,
            JsonSerializerOptions options) => new() {Value = reader.GetString() ?? string.Empty};

        public override void Write(Utf8JsonWriter writer, TBbText bbText, JsonSerializerOptions options)
        {
            var httpContext = httpContextAccessor.HttpContext!;
            var renderMode = httpContext.Request.Headers.TryGetValue("X-Dm-Bb-Render-Mode", out var headerValues) &&
                             headerValues.Any() && Enum.TryParse<BbRenderMode>(headerValues.First(), out var requiredRenderMode)
                ? requiredRenderMode
                : BbRenderMode.Html;
            var value = bbText.Value;
                
            if (renderMode == BbRenderMode.SafeHtml)
            {
                var safeParsedTree = bbParserProvider.CurrentSafePost.Parse(value);
                var safeHtml = safeParsedTree is BbParserWrapper.WrappedNodeTree safeWrappedTree
                    ? safeWrappedTree.ToHtml()
                    : safeParsedTree.ToHtml();
                writer.WriteStringValue(safeHtml);
                return;
            }
                
            var parsedTree = bbText.ParseMode switch
            {
                BbParseMode.Common => bbParserProvider.CurrentCommon.Parse(value),
                BbParseMode.Info => bbParserProvider.CurrentInfo.Parse(value),
                BbParseMode.Post => bbParserProvider.CurrentPost.Parse(value),
                _ => throw new ArgumentOutOfRangeException(nameof(bbText.ParseMode))
            };
                
            // Handle WrappedNodeTree (from BbParserWrapper) which has custom ToHtml/ToBb/ToText methods
            var text = parsedTree is BbParserWrapper.WrappedNodeTree wrappedTree
                ? renderMode switch
                {
                    BbRenderMode.Html => wrappedTree.ToHtml(),
                    BbRenderMode.Bb => wrappedTree.ToBb(),
                    BbRenderMode.Text => wrappedTree.ToText(),
                    _ => throw new ArgumentOutOfRangeException()
                }
                : renderMode switch
                {
                    BbRenderMode.Html => parsedTree.ToHtml(),
                    BbRenderMode.Bb => parsedTree.ToBb(),
                    BbRenderMode.Text => parsedTree.ToText(),
                    _ => throw new ArgumentOutOfRangeException()
                };
                
            writer.WriteStringValue(text);
        }
    }
}