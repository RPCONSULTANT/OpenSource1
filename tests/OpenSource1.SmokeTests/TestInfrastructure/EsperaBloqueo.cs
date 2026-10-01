using Dapper;
using Npgsql;

namespace OpenSource1.SmokeTests.TestInfrastructure;

/// <summary>
/// Pruebas de bloqueos contra Postgres real: espera a que alguna sesión quede bloqueada por la sesión <c>pid</c> que retiene el
/// bloqueo, sin depender de una demora fija (en frío, una tarea podía arrancar después del commit y la prueba pasar o fallar por
/// tiempos). Si la tarea termina antes de quedar bloqueada, no respetó el bloqueo: la prueba falla.
/// </summary>
internal static class EsperaBloqueo
{
    public static async Task EsperarBloqueadaPorAsync(string connectionString, int pid, Task tarea, string mensaje)
    {
        await using var monitor = new NpgsqlConnection(connectionString);
        await monitor.OpenAsync();
        var limite = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < limite)
        {
            if (await monitor.ExecuteScalarAsync<bool>(
                    "SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE @Pid = ANY(pg_blocking_pids(pid)))", new { Pid = pid }))
            {
                return;
            }

            Assert.False(tarea.IsCompleted, $"{mensaje} (terminó sin esperar el bloqueo).");
            await Task.Delay(50);
        }

        Assert.Fail($"{mensaje} (no llegó a esperar el bloqueo).");
    }
}
