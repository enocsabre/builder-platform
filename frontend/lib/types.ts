export type ProductStatus =
  | "Draft" | "Discovering" | "Architecting" | "Planning"
  | "Building" | "Reviewing" | "Stable" | "Error";

export type DeployStatus =
  | "not_deployed" | "preparing" | "building" | "deploying" | "deployed" | "failed" | "recovering";

export type RuntimePhase =
  | "idle" | "queued" | "discovery" | "architecting" | "planning"
  | "waiting_approval" | "building" | "reviewing";

export type MessageRole = "User" | "Runtime" | "System";
export type ApprovalStatus = "Pending" | "Approved" | "Rejected";
export type ArtifactStatus = "Draft" | "Approved" | "Superseded";
export type ArtifactType = "brief" | "architecture" | "db_schema" | "roadmap" | "sprint_plan";
export type ScaffoldStatus  = "none" | "generating" | "complete" | "error";
export type PreviewStatus   = "stopped" | "starting" | "running" | "error";
export type RuntimeHealth   = "healthy" | "degraded" | "broken" | "recovering";
export type ActivityType    = string;
export type FileType        = "json" | "tsx" | "css";

export interface ScaffoldEntry {
  id:           string;
  relativePath: string;
  entryType:    "file" | "directory";
  language:     string | null;
  sortOrder:    number;
}

export interface GateResult {
  gate:     string;
  category: string;
  passed:   boolean;
  skipped:  boolean;
  message:  string;
  detail:   string | null;
}

export interface DeployRunSummary {
  id:         string;
  status:     string;
  startedAt:  string;
  finishedAt: string | null;
  deployUrl:  string | null;
  commitHash: string | null;
  branch:     string | null;
}

export interface DeployRunDetail extends DeployRunSummary {
  logs:        string | null;
  errors:      string | null;
  gateResults: GateResult[];
}

export interface ValidationRunSummary {
  id:             string;
  status:         string;
  startedAt:      string;
  finishedAt:     string | null;
  autofixAttempts: number;
  gatesPassed:    number;
  gatesFailed:    number;
}

export interface ValidationRunDetail extends ValidationRunSummary {
  logs:        string | null;
  errors:      string | null;
  gateResults: GateResult[];
}

export interface ProductSummary {
  id:             string;
  name:           string;
  status:         ProductStatus;
  previewUrl:     string | null;
  previewStatus:  PreviewStatus;
  previewPort:    number | null;
  isProcessing:   boolean;
  runtimePhase:   string;
  projectPath:    string | null;
  scaffoldStatus: ScaffoldStatus;
  runtimeHealth:  RuntimeHealth;
  createdAt:      string;
  updatedAt:      string;
  deployStatus:   DeployStatus;
  deployUrl:      string | null;
  deployedAt:     string | null;
  industryKey:    string | null;
}

export interface Message {
  id:             string;
  role:           MessageRole;
  content:        string;
  detectedIntent: string | null;
  confidence:     number | null;
  createdAt:      string;
}

export interface Activity {
  id:         string;
  eventType:  ActivityType;
  title:      string;
  details:    string | null;
  artifactId: string | null;
  createdAt:  string;
}

export interface Approval {
  id:             string;
  title:          string;
  description:    string;
  status:         ApprovalStatus;
  resolutionNote: string | null;
  artifactId:     string | null;
  createdAt:      string;
  resolvedAt:     string | null;
}

export interface ArtifactSummary {
  id:          string;
  type:        ArtifactType;
  title:       string;
  version:     number;
  status:      ArtifactStatus;
  generatedAt: string;
}

export interface Artifact extends ArtifactSummary {
  content: string;
}

export interface MemoryEntry {
  key:       string;
  value:     string;
  createdAt: string;
}

export interface ScaffoldChange {
  id:          string;
  changeType:  "created" | "skipped";
  targetPath:  string;
  moduleLabel: string;
  layer:       "backend" | "frontend";
  createdAt:   string;
}

export interface ProductModule {
  id:             string;
  moduleName:     string;
  entityName:     string;
  routePath:      string;
  controllerName: string;
  layer:          string;
  source:         "scaffold" | "delta";
  isActive:       boolean;
  detectedAt:     string;
}

export interface FileRevision {
  id:           string;
  relativePath: string;
  patchType:    string;
  reason:       string;
  hasDiff:      boolean;
  createdAt:    string;
}

export interface ManagedFile {
  relativePath:  string;
  displayName:   string;
  fileType:      FileType;
  exists:        boolean;
  lastModified:  string | null;
  isEditable:    boolean;
  revisionCount: number;
}

export interface ProductDetail extends ProductSummary {
  prompt:               string;
  previewLastStartedAt: string | null;
  previewError:         string | null;
  messages:             Message[];
  activity:             Activity[];
  approvals:            Approval[];
  memory:               MemoryEntry[];
  artifacts:            ArtifactSummary[];
  scaffoldEntries:      ScaffoldEntry[];
  scaffoldChanges:      ScaffoldChange[];
  modules:              ProductModule[];
  fileRevisions:        FileRevision[];
  validationRuns:       ValidationRunSummary[];
  deployCommitHash:         string | null;
  deployBranch:             string | null;
  deployRuns:               DeployRunSummary[];
  refactorRecommendations:  RefactorRecommendation[];
}

export const PROCESSING_STATUSES: ProductStatus[] = ["Discovering", "Architecting", "Planning"];
export const ACTIVE_STATUSES:     ProductStatus[] = ["Building", "Reviewing"];

export interface EvolutionModule {
  name:    string;
  route:   string;
  layer:   string;
  addedAt: string;
}

export interface EvolutionRelation {
  from:         string;
  to:           string;
  relationType: string;
  reason:       string;
  detectedAt:   string;
}

export interface EvolutionDecision {
  summary: string;
  madeAt:  string;
}

export interface EvolutionContext {
  modules:        EvolutionModule[];
  relations:      EvolutionRelation[];
  decisions:      EvolutionDecision[];
  featureHistory: string[];
}

export interface SimulationStatus {
  isRunning:    boolean;
  scenario:     string | null;
  opsGenerated: number;
  runId:        string | null;
  startedAt:    string | null;
}

export const SIMULATION_SCENARIO_LABELS: Record<string, string> = {
  hora_pico:            "Hora pico",
  cocina_congestionada: "Cocina congestionada",
  bajo_inventario:      "Bajo inventario",
  operacion_normal:     "Operación normal",
};

export interface DemoResetResult {
  reset:   boolean;
  message: string;
}

// ── Intelligence types ─────────────────────────────────────────────────────────

export interface IntelligenceGap {
  module:   string;
  reason:   string;
  priority: "high" | "medium" | "low";
  category: string;
}

export interface IntelligenceConnection {
  from:     string;
  to:       string;
  label:    string;
  detected: boolean;
  impact:   string;
}

export interface IntelligenceSuggestion {
  title:    string;
  context:  string;
  impact:   string;
  category: string;
}

export interface ProactiveInsight {
  type:     "critical_gap" | "missing_connection" | "gap_warning" | "evolution" | "stalled";
  severity: "high" | "medium" | "low";
  title:    string;
  detail:   string;
  action:   string;
  // Sprint 40
  daysSinceDetectable: number;
  insightStage:        "new" | "observed" | "persistent" | "critical";
}

export interface IntelligenceReport {
  productId:             string;
  industry:              string;
  industryLabel:         string;
  moduleCount:           number;
  evolutionStage:        "starter" | "growth" | "mature";
  evolutionStageLabel:   string;
  evolutionNextMilestone:string;
  gaps:                  IntelligenceGap[];
  connections:           IntelligenceConnection[];
  suggestions:           IntelligenceSuggestion[];
  narrative:             string;
  analyzedAt:            string;
  // Sprint 39
  healthScore:           "starter" | "operational" | "growing" | "mature";
  healthScoreLabel:      string;
  healthScoreNumeric:    number;
  criticalCount:         number;
  topInsights:           ProactiveInsight[];
  // Sprint 40
  productAgeDays:        number;
  gapAgeDays:            number;
  operationalDebtCount:  number;
  recentModuleCount:     number;
  pendingRefactorCount:  number;
}

// ── Roadmap types (Sprint 41) ──────────────────────────────────────────────

export interface RoadmapMilestone {
  id:              string;
  title:           string;
  phase:           "now" | "next" | "later";
  priority:        "critical" | "high" | "medium";
  category:        "core" | "integration" | "growth" | "analytics";
  why:             string;
  unlocks:         string;
  requiredModules: string[];
}

export interface RoadmapDependency {
  from:   string;
  to:     string;
  reason: string;
}

export interface StrategicRoadmap {
  productId:            string;
  industry:             string;
  industryLabel:        string;
  completionScore:      number;
  totalCheckpoints:     number;
  completedCheckpoints: number;
  growthNarrative:      string;
  nextFocusTitle:       string;
  nextFocusWhy:         string;
  milestones:           RoadmapMilestone[];
  dependencies:         RoadmapDependency[];
  generatedAt:          string;
}

export interface RefactorRecommendation {
  id:             string;
  type:           string;
  title:          string;
  severity:       "low" | "medium" | "high";
  reason:         string;
  impact:         string;
  risk:           string;
  status:         "pending" | "accepted" | "rejected" | "applied" | "failed";
  note:           string | null;
  artifactId:     string | null;
  createdAt:      string;
  resolvedAt:     string | null;
  executedAt:     string | null;
  executionError: string | null;
}
