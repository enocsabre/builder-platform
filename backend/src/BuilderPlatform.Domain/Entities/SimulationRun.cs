namespace BuilderPlatform.Domain.Entities;

public class SimulationRun
{
    public Guid      Id          { get; set; } = Guid.NewGuid();
    public Guid      ProductId   { get; set; }
    // "running" | "stopped" | "completed"
    public string    Status      { get; set; } = "running";
    // "hora_pico" | "cocina_congestionada" | "bajo_inventario" | "operacion_normal"
    public string    Scenario    { get; set; } = "operacion_normal";
    public int       OpsGenerated { get; set; } = 0;
    public DateTime  StartedAt   { get; set; } = DateTime.UtcNow;
    public DateTime? StoppedAt   { get; set; }

    public Product Product { get; set; } = null!;
}
