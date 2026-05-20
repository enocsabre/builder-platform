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
  deployCommitHash:     string | null;
  deployBranch:         string | null;
  deployRuns:           DeployRunSummary[];
}

export const PROCESSING_STATUSES: ProductStatus[] = ["Discovering", "Architecting", "Planning"];
export const ACTIVE_STATUSES:     ProductStatus[] = ["Building", "Reviewing"];
