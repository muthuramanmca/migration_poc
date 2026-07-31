namespace BuildingBlocks.Common;

/// <summary>
/// Mark any property that can hold PII (passenger name, DOB, passport number, email, phone, etc.)
/// with this attribute. <see cref="SensitiveDataDestructuringPolicy"/> redacts it automatically
/// wherever the containing object is logged, so "never log PII" is a guardrail from day one --
/// it applies the moment a real entity/DTO adds a sensitive field, with no logging call sites to update.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SensitiveDataAttribute : Attribute;
