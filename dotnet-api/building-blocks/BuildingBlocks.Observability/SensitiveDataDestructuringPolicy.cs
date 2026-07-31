using System.Reflection;
using BuildingBlocks.Common;
using Serilog.Core;
using Serilog.Events;

namespace BuildingBlocks.Observability;

/// <summary>
/// Serilog destructuring policy: any property carrying <see cref="SensitiveDataAttribute"/> is replaced
/// with a redaction marker before the containing object is written to any sink.
/// </summary>
public sealed class SensitiveDataDestructuringPolicy : IDestructuringPolicy
{
    private const string RedactedPlaceholder = "***REDACTED***";

    public bool TryDestructure(object value, ILogEventPropertyValueFactory propertyValueFactory, out LogEventPropertyValue? result)
    {
        var type = value.GetType();
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var hasSensitiveProperty = properties.Any(p => p.GetCustomAttribute<SensitiveDataAttribute>() is not null);

        if (!hasSensitiveProperty)
        {
            result = null;
            return false;
        }

        var logProperties = properties.Select(p =>
        {
            var isSensitive = p.GetCustomAttribute<SensitiveDataAttribute>() is not null;
            var propertyValue = isSensitive
                ? new ScalarValue(RedactedPlaceholder)
                : propertyValueFactory.CreatePropertyValue(SafeGetValue(p, value), destructureObjects: true);
            return new LogEventProperty(p.Name, propertyValue);
        });

        result = new StructureValue(logProperties, type.Name);
        return true;
    }

    private static object? SafeGetValue(PropertyInfo property, object instance)
    {
        try
        {
            return property.GetValue(instance);
        }
        catch
        {
            return null;
        }
    }
}
