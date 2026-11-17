namespace ClosedValues
{
    /// <summary>
    /// Everything the generator needs to write the source for one closed value type.
    /// </summary>
    internal sealed record ClosedValueModel(
        string Namespace,
        string TypeName,
        bool IsRecord,
        bool IsValueType,
        bool SupportsNullable,
        bool HasNotNullWhenAttribute,
        bool DeclaresToString,
        ClosedValueConverters Converters,
        EquatableArray<ClosedValueMember> Members,
        EquatableArray<string> Instances)
    {
        public string HintName => (Namespace.Length > 0 ? Namespace + "." : string.Empty) + TypeName + ClosedValueNames.GeneratedFileSuffix;
    }

    /// <summary>
    /// One get-only auto-property of a closed value type, in declaration order.
    /// </summary>
    /// <param name="Kind">The special type without its <c>System_</c> prefix, such as <c>Int32</c>, or <c>Enum</c>.</param>
    internal sealed record ClosedValueMember(string Name, string Type, bool IsString, bool IsBasicType, bool IsOrdered, string Kind);

    /// <summary>
    /// Which converters the generator writes. A converter class needs its base type in the compilation, and its
    /// attribute is skipped when the type already has an own attribute of that kind.
    /// </summary>
    internal sealed record ClosedValueConverters(
        bool TypeConverter,
        bool TypeConverterAttribute,
        bool JsonConverter,
        bool JsonConverterAttribute,
        bool JsonPropertyNames);
}
