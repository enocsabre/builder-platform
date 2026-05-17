namespace BuilderPlatform.Infrastructure.Services;

public enum Intent
{
    CreateProduct,
    FeatureRequest,
    UiRefinement,
    BugFix,
    Stabilization,
    Refactor,
    DeploymentRequest,
    DashboardRequest,
    UiEvolution,
    ValidateRequest,
    Unknown,
}

public record IntentResult(Intent Intent, double Confidence, string Reasoning);

public static class IntentEngine
{
    private static readonly (string[] Keywords, Intent Intent, string Reasoning)[] Rules =
    [
        (["deploy", "producción", "lanzar", "publicar", "subir a prod", "ir a prod", "staging", "release"],
            Intent.DeploymentRequest, "Solicitud de deploy detectada"),

        (["bug", "error", "falla", "roto", "no funciona", "no carga", "broken", "arreglar", "fix", "crash"],
            Intent.BugFix, "Reporte de bug o problema funcional"),

        (["refactorizar", "refactor", "limpiar código", "reorganizar", "restructurar", "deuda técnica"],
            Intent.Refactor, "Solicitud de refactoring o limpieza técnica"),

        (["estabilizar", "optimizar", "mejorar rendimiento", "lento", "performance", "mejorar velocidad"],
            Intent.Stabilization, "Solicitud de estabilización o mejora de performance"),

        ([" ui ", " ux ", "diseño", "interfaz", "pantalla", "colores", "botón", "layout", "estilo", "componente visual", "responsive", "cambiar diseño", "mejorar diseño"],
            Intent.UiRefinement, "Ajuste o mejora de interfaz de usuario"),

        (["valida el proyecto", "corre los quality gates", "corre los gates", "revisa el estado",
          "valida el estado", "checkea el estado", "health check", "quality gates", "validar",
          "corre la validación", "verifica el build", "verifica el proyecto"],
            Intent.ValidateRequest, "Solicitud de validación y quality gates"),

        (["más premium", "mas premium", "haz premium", "look premium", "aspecto premium",
          "pon el dashboard primero", "primero en el sidebar", "primero en el nav",
          "reordena el sidebar", "reordena la navegación", "reordena el nav",
          "quick stats", "estadísticas rápidas", "stats arriba", "métricas rápidas",
          "mejora visual", "mejora el diseño del dash", "actualiza el diseño"],
            Intent.UiEvolution, "Evolución de UI — mejora visual, reordenamiento o layout"),

        (["al dashboard", "en el dashboard", "en dashboard", "al dash",
          "dashboard widget", "nuevo widget", "nueva métrica", "métricas al", "ventas al", "alertas al",
          "actualiza el dashboard", "actualizar el dashboard", "widget de", "widget para"],
            Intent.DashboardRequest, "Actualización de dashboard — widget o métrica solicitada"),

        (["agregar", "agrega", "agregá", "añadir", "añade", "añadí", "incluir", "nueva feature", "nueva funcionalidad",
          "nuevo módulo", "quiero que tenga", "necesito", "implementar", "crear módulo", "quiero alertas",
          "quiero reportes", "quiero roles", "quiero membresías", "quiero notificaciones"],
            Intent.FeatureRequest, "Solicitud de nueva feature o módulo"),
    ];

    public static IntentResult Classify(string text)
    {
        var lower = text.ToLowerInvariant();
        var hits  = new Dictionary<Intent, (int Count, string Reasoning)>();

        foreach (var (keywords, intent, reasoning) in Rules)
        {
            var count = keywords.Count(k => lower.Contains(k));
            if (count > 0) hits[intent] = (count, reasoning);
        }

        if (hits.Count == 0)
            return new(Intent.Unknown, 0.4, "No se detectó intención específica — procesando como consulta general");

        var best       = hits.MaxBy(kv => kv.Value.Count);
        var confidence = best.Value.Count >= 2 ? 0.92 : 0.74;
        return new(best.Key, confidence, best.Value.Reasoning);
    }

    public static string ToLabel(Intent intent) => intent switch
    {
        Intent.CreateProduct     => "create_product",
        Intent.FeatureRequest    => "feature_request",
        Intent.UiRefinement      => "ui_refinement",
        Intent.BugFix            => "bug_fix",
        Intent.Stabilization     => "stabilization",
        Intent.Refactor          => "refactor",
        Intent.DeploymentRequest => "deployment_request",
        Intent.DashboardRequest  => "dashboard_request",
        Intent.UiEvolution       => "ui_evolution",
        Intent.ValidateRequest   => "validate_request",
        _                        => "unknown",
    };
}
