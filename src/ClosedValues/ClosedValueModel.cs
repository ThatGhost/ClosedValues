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
        EquatableArray<ClosedValueMember> Members,
        EquatableArray<string> Instances)
    {
        public string HintName => (Namespace.Length > 0 ? Namespace + "." : string.Empty) + TypeName + ClosedValueNames.GeneratedFileSuffix;
    }

    /// <summary>
    /// One get-only auto-property of a closed value type, in declaration order.
    /// </summary>
    internal sealed record ClosedValueMember(string Name, string Type, bool IsString, bool IsBasicType);
}
