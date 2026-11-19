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
        bool IsParsable,
        ClosedValueConverters Converters,
        ClosedValueSwitches TypeSwitches,
        ClosedValueSwitches ProjectSwitches,
        EquatableArray<ClosedValueMember> Members,
        EquatableArray<string> Instances)
    {
        public string HintName => (Namespace.Length > 0 ? Namespace + "." : string.Empty) + TypeName + ClosedValueNames.GeneratedFileSuffix;

        /// <summary>The switches that apply: a setting on the type wins over the project setting.</summary>
        public ClosedValueSwitches Switches => TypeSwitches.Over(ProjectSwitches);

        public bool WritesTypeConverterAttribute => Converters.TypeConverterAttribute && Switches.TypeConverter != false;

        public bool WritesJsonConverter => Converters.JsonConverter && Switches.Json != false;

        public bool WritesJsonConverterAttribute => WritesJsonConverter && Converters.JsonConverterAttribute;

        public bool WritesParsable => IsParsable && Switches.Parsable != false;

        public bool WritesEfCoreConverter => Converters.EfCore && Switches.EfCore != false;
    }

    /// <summary>
    /// The integration switches. A null value is not set, which means on unless a fallback sets it.
    /// </summary>
    internal sealed record ClosedValueSwitches(bool? Json, bool? TypeConverter, bool? Parsable, bool? EfCore)
    {
        public static readonly ClosedValueSwitches None = new(null, null, null, null);

        public ClosedValueSwitches Over(ClosedValueSwitches fallback) =>
            new(Json ?? fallback.Json, TypeConverter ?? fallback.TypeConverter, Parsable ?? fallback.Parsable, EfCore ?? fallback.EfCore);
    }

    /// <summary>
    /// One get-only auto-property of a closed value type, in declaration order.
    /// </summary>
    /// <param name="Kind">The special type without its <c>System_</c> prefix, such as <c>Int32</c>, or <c>Enum</c>.</param>
    /// <param name="IsKey">Whether this member is the key: marked with [ClosedValueKey], or the single basic member.</param>
    internal sealed record ClosedValueMember(string Name, string Type, bool IsString, bool IsBasicType, bool IsOrdered, string Kind, bool IsKey);

    /// <summary>
    /// Which converters the generator writes. A converter class needs its base type in the compilation, and its
    /// attribute is skipped when the type already has an own attribute of that kind.
    /// </summary>
    internal sealed record ClosedValueConverters(
        bool TypeConverter,
        bool TypeConverterAttribute,
        bool JsonConverter,
        bool JsonConverterAttribute,
        bool JsonPropertyNames,
        bool EfCore,
        bool EfCoreConventions);
}
