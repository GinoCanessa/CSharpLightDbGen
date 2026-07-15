using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace CsLightDbGen.SQLiteGenerator;

/// <summary>
/// Whether a generated model is declared as a <c>class</c> or a <c>record class</c>. Extracted into
/// the equatable pipeline model so emission never needs the original symbol.
/// </summary>
internal enum LdGenCategory
{
    Class,
    Record,
}

/// <summary>
/// A value-equatable wrapper over <see cref="ImmutableArray{T}"/>. <see cref="ImmutableArray{T}"/>
/// only offers reference equality, which defeats incremental-generator caching when it is embedded
/// in a pipeline model; this type compares elements structurally so an unchanged model produces a
/// cache hit.
/// </summary>
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IReadOnlyList<T>
    where T : IEquatable<T>
{
    public static readonly EquatableArray<T> Empty = new(ImmutableArray<T>.Empty);

    private readonly ImmutableArray<T> _array;

    public EquatableArray(ImmutableArray<T> array)
    {
        _array = array;
    }

    private ImmutableArray<T> Array => _array.IsDefault ? ImmutableArray<T>.Empty : _array;

    public int Count => Array.Length;

    public T this[int index] => Array[index];

    public bool Equals(EquatableArray<T> other)
    {
        ImmutableArray<T> left = Array;
        ImmutableArray<T> right = other.Array;

        if (left.Length != right.Length)
        {
            return false;
        }

        for (int i = 0; i < left.Length; i++)
        {
            if (!EqualityComparer<T>.Default.Equals(left[i], right[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            foreach (T item in Array)
            {
                hash = (hash * 31) + (item is null ? 0 : item.GetHashCode());
            }

            return hash;
        }
    }

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)Array).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right) => left.Equals(right);

    public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right) => !left.Equals(right);
}

/// <summary>Helpers for building <see cref="EquatableArray{T}"/> values.</summary>
internal static class EquatableArrayExtensions
{
    public static EquatableArray<T> ToEquatableArray<T>(this IEnumerable<T> source)
        where T : IEquatable<T> =>
        new(ImmutableArray.CreateRange(source));
}

/// <summary>
/// A value-equatable projection of a <see cref="Location"/> (source file + spans). Storing the
/// spans rather than the <see cref="SyntaxReference"/>/<see cref="Location"/> keeps the pipeline
/// model free of syntax while still allowing a real <see cref="Location"/> to be reconstructed when
/// a diagnostic is reported from the source-output stage.
/// </summary>
internal record struct LocationInfo(string FilePath, TextSpan TextSpan, LinePositionSpan LineSpan)
{
    public Location ToLocation() => Location.Create(FilePath, TextSpan, LineSpan);

    public static LocationInfo? From(ISymbol? symbol) =>
        symbol is null || symbol.Locations.Length == 0 ? null : From(symbol.Locations[0]);

    public static LocationInfo? From(Location? location) =>
        location?.SourceTree is null
            ? null
            : new LocationInfo(location.SourceTree.FilePath, location.SourceSpan, location.GetLineSpan().Span);
}

/// <summary>
/// A fully value-equatable description of one table column, extracted from a property symbol and its
/// declaration syntax during the incremental pipeline's transform step. Emission consumes only these
/// fields, so it never touches a <see cref="ISymbol"/> or <see cref="Compilation"/>.
/// </summary>
internal record struct ColumnInput(
    string Name,
    string TypeName,
    bool Nullable,
    bool IsKey,
    bool KeyAutoIncrement,
    bool KeyAutoIncrementExplicit,
    bool IsUnique,
    bool IsUnindexed,
    bool HasMultiSelectAttr,
    bool IsValueType,
    bool IsEnum,
    string? EnumTypeName,
    bool IsNonScalar,
    bool IsArray,
    string? JsonTypeName,
    string DefaultClause,
    bool DefaultIsRaw,
    string? ForeignTable,
    string? ForeignColumn,
    string? ForeignModelType,
    string FkActions,
    LocationInfo? Location);

/// <summary>A value-equatable description of a class-level <c>[LdgSQLiteIndex]</c>.</summary>
internal record struct IndexInfo(EquatableArray<string> Columns, bool Unique, string? Where);

/// <summary>
/// The complete, value-equatable input for generating a regular table's DAL. Produced by the
/// pipeline transform and consumed by emission; because every field is equatable, an unchanged model
/// yields a cache hit and its source output is not regenerated.
/// </summary>
internal record struct TableModel(
    string ClassName,
    string? ClassNamespace,
    string TableName,
    LdGenCategory GenCategory,
    LocationInfo? Location,
    EquatableArray<ColumnInput> Columns,
    EquatableArray<string> CompositeFkLines,
    EquatableArray<EquatableColumnSet> ClassUniqueColumnSets,
    EquatableArray<IndexInfo> Indexes);

/// <summary>
/// A value-equatable set of column names (used for class-level composite UNIQUE constraints). A thin
/// wrapper so it can live inside an <see cref="EquatableArray{T}"/> (whose element must be
/// <see cref="IEquatable{T}"/>).
/// </summary>
internal record struct EquatableColumnSet(EquatableArray<string> Columns);

/// <summary>
/// The complete, value-equatable input for generating an FTS5 virtual table's DAL. FTS models carry
/// no keys, foreign keys, indexes, or defaults, so this is a reduced shape compared with
/// <see cref="TableModel"/>.
/// </summary>
internal record struct FtsModel(
    string ClassName,
    string? ClassNamespace,
    string TableName,
    string SourceTableName,
    string? Tokenizer,
    LdGenCategory GenCategory,
    LocationInfo? Location,
    EquatableArray<ColumnInput> Columns);
