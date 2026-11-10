using System;
using System.Linq;
using System.Reflection;

namespace DM.Infrastructure.Core.Extensions;

/// <summary>
/// Attribute helpers
/// </summary>
public static class AttributeExtensions
{
    public static TAttr GetAttribute<TStruct, TAttr>(this TStruct structValue)
        where TStruct : struct
        where TAttr : Attribute
    {
        var attributes = GetAllAttributes<TStruct, TAttr>(structValue);
        if (attributes.Length > 1)
            throw new AmbiguousAttributesException(typeof(TAttr));
        if (attributes.Length == 1)
            return attributes[0];
        throw new AttributeNotFoundException(typeof(TAttr), typeof(TStruct));
    }

    private static TAttr[] GetAllAttributes<TStruct, TAttr>(this TStruct structValue)
        where TStruct : struct
        where TAttr : Attribute
    {
        var type = typeof(TStruct);
        var isFlag = type.GetCustomAttribute<FlagsAttribute>() != null;
        var attributes = new TAttr[0];
        foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.GetField | BindingFlags.Public))
        {
            var fieldValue = field.GetValue(null);
            if (fieldValue is null) continue;
            if (fieldValue.Equals(structValue))
                return (TAttr[])field.GetCustomAttributes(typeof(TAttr), false);
            if (isFlag && type.IsEnum)
            {
                // This feels like a black magic
                var enumValue = (object)structValue;
                if (((int)fieldValue & (int)enumValue) != 0)
                {
                    attributes = attributes.Concat((TAttr[])field.GetCustomAttributes(typeof(TAttr), false)).ToArray();
                }
            }
        }
        return attributes;
    }
}

internal class AmbiguousAttributesException : Exception
{
    public AmbiguousAttributesException(Type attrType)
        : base($"Ambiguous attributes of type {attrType.FullName} found.")
    {
    }
}

public class AttributeNotFoundException : Exception
{
    public AttributeNotFoundException(Type attrType, Type enumType)
        : base($"Attribute of type {attrType.FullName} not found at enum {enumType.FullName}.")
    {
    }
}
