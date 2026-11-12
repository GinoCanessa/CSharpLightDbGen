using System.Collections.Generic;
using System.Linq;

namespace CsLightDbGen.SQLiteGenerator;

internal static class TableSchemaPlanner
{
    public static TableSchemaPlan Plan(EquatableArray<ColumnInput> columns)
    {
        EquatableArray<ColumnInput> keys = columns.Where(column => column.IsKey).ToEquatableArray();
        ColumnInput? singleKey = keys.Count == 1 ? keys[0] : null;
        bool isIdentity = singleKey is ColumnInput key &&
            (key.TypeName == "int" || key.TypeName == "long") && key.KeyAutoIncrement;
        List<ColumnSchemaPlan> columnPlans = [];
        foreach (ColumnInput column in columns)
        {
            bool inlinePrimaryKey = column.IsKey && keys.Count == 1;
            bool defaultInlineKey = inlinePrimaryKey && column.KeyStyle == KeyConstraintStyle.Default;
            columnPlans.Add(new ColumnSchemaPlan(
                column,
                column.IsKey,
                column.IsKey && isIdentity,
                inlinePrimaryKey,
                defaultInlineKey,
                defaultInlineKey,
                column.KeyStyle != KeyConstraintStyle.PrimaryKeyOnly && !column.Nullable && !inlinePrimaryKey));
        }

        return new TableSchemaPlan(columnPlans.ToEquatableArray(), keys, singleKey, isIdentity);
    }
}
