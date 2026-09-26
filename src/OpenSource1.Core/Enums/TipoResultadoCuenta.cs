namespace OpenSource1.Core.Enums;

/// <summary>
/// Estado financiero al que pertenece una cuenta contable: estado de resultados o balance general
/// (Fase 5, Task 5.2). Se guarda como <c>smallint</c>.
/// </summary>
public enum TipoResultadoCuenta : short
{
    Resultado = 1,
    Balance = 2
}
