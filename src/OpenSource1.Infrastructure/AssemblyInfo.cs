using System.Runtime.CompilerServices;

// Permite que OpenSource1.SmokeTests pruebe tipos `internal` (p. ej. ColumnasPermitidas y
// FilterExpressionBuilder) sin ampliar su superficie pública fuera de este ensamblado.
[assembly: InternalsVisibleTo("OpenSource1.SmokeTests")]
