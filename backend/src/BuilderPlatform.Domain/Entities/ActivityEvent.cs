namespace BuilderPlatform.Domain.Entities;

public class ActivityEvent
{
    public Guid          Id         { get; set; } = Guid.NewGuid();
    public Guid          ProductId  { get; set; }
    public ActivityType  EventType  { get; set; }
    public string        Title      { get; set; } = string.Empty;
    public string?       Details    { get; set; }
    public Guid?         ArtifactId { get; set; }
    public DateTime      CreatedAt  { get; set; } = DateTime.UtcNow;

    public Product  Product  { get; set; } = null!;
    public Artifact? Artifact { get; set; }
}

public enum ActivityType
{
    ProductCreated,
    DiscoveryStarted,
    BriefGenerated,
    ApprovalPending,
    ApprovalResolved,
    ArchitectureGenerated,
    SprintStarted,
    SprintCompleted,
    BuildComplete,
    ArtifactGenerated,
    ScaffoldStarted,
    ScaffoldCompleted,
    FeatureDetected,
    ScaffoldDeltaStarted,
    BackendModuleGenerated,
    FrontendModuleGenerated,
    NavigationUpdated,
    RuntimeReviewCompleted,
    ProjectScanned,
    RegistryUpdated,
    DashboardWidgetAdded,
    NavigationItemAdded,
    PreviewStarting,
    PreviewRunning,
    PreviewStopped,
    PreviewError,
    RuntimePatchStarted,
    RuntimeFileUpdated,
    PreviewRestarted,
    RuntimePatchSkipped,
    RuntimePatchFailed,
    ValidationStarted,
    ValidationPassed,
    ValidationFailed,
    AutofixStarted,
    AutofixSucceeded,
    AutofixFailed,
    RuntimeRecovered,
    DeployStarted,
    DeployBuildStarted,
    DeployBuildPassed,
    DeployFailed,
    DeploySucceeded,
    DeployRecoveryStarted,
    ErrorOccurred,
    StatusChanged,
    MessageSent,
    VSCodeOpenRequested,
    VSCodeOpenSucceeded,
    VSCodeOpenFailed,
    EvolutionRelationsDetected,
    EvolutionMemoryUpdated,
    RefactorDetected,
    RefactorAccepted,
    RefactorRejected,
    RefactorExecutionStarted,
    RefactorFileUpdated,
    RefactorValidationPassed,
    RefactorRollbackStarted,
    RefactorRollbackCompleted,
    RefactorExecutionFailed,
    RefactorExecutionSucceeded,
    SimulationStarted,
    SimulationOperationGenerated,
    SimulationStopped,
    SimulationCompleted,
}
