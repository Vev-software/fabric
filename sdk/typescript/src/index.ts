export const CONTRACT_VERSION = "1" as const;

export type ReasonCode =
  | "allow"
  | "role_missing"
  | "entitlement_granted"
  | "entitlement_denied"
  | "entitlement_unavailable"
  | "entitlement_snapshot_invalid"
  | "entitlement_snapshot_stale"
  | "entitlement_snapshot_tenant_mismatch"
  | "lifecycle_trial_expired"
  | "lifecycle_read_only"
  | "lifecycle_locked"
  | "lifecycle_retention"
  | "lifecycle_purged"
  | "lifecycle_transition_invalid";

export type EntitlementOffer =
  | "CommunitySelfHosted"
  | "HostedTrial"
  | "HostedStarter"
  | "Pro"
  | "Enterprise"
  | "SelfHostedEnterprise";

export type EntitlementLifecycleState =
  | "Active"
  | "TrialActive"
  | "TrialExpired"
  | "ReadOnly"
  | "Locked"
  | "RetentionPeriod"
  | "DataPurged";

export type TenantLifecycleState =
  | "TrialActive"
  | "TrialExpired"
  | "ReadOnly"
  | "Locked"
  | "RetentionPeriod"
  | "DataPurged";

export type TenantLifecycleTransition =
  | "EnterReadOnly"
  | "Lock"
  | "StartRetention"
  | "PurgeData";
export type DiscoveryEnrollmentState =
  | "Pending"
  | "Active"
  | "Suspended"
  | "Revoked"
  | "Expired";

export type DiscoveryEnrollmentTransition =
  | "Activate"
  | "RotateCredential"
  | "Suspend"
  | "Revoke";

export type DiscoveryLifecycleEventType =
  | "EnrollmentCreated"
  | "EnrollmentActivated"
  | "CredentialRotated"
  | "AccessDenied"
  | "EnrollmentSuspended"
  | "EnrollmentRevoked"
  | "CredentialExpired";
export type TaxonomyKind = "Feature" | "Limit" | "Resource" | "Reason";

export interface TenantContext {
  tenantId: string;
}

export interface PrincipalContext {
  principalId: string;
  displayName?: string | null;
  roles: string[];
  /** Optional provider-neutral claims (e.g. selected OIDC claims). Never secrets or tokens. */
  claims?: Record<string, string> | null;
}

export interface CapabilityId {
  value: string;
}

export interface ResourceId {
  value: string;
}

export interface EntitlementRequest {
  tenant: TenantContext;
  capability: CapabilityId;
  principal: PrincipalContext;
  resource?: ResourceId | null;
}

export interface AuthorizationRequest {
  tenant: TenantContext;
  principal: PrincipalContext;
  action: string;
  resource: ResourceId;
}

export interface AuthorizationDecision {
  allowed: boolean;
  action: string;
  resource: ResourceId;
  reasonCode: ReasonCode | string;
  source: string;
}

export interface EntitlementDecision {
  allowed: boolean;
  capability: CapabilityId;
  reasonCode: ReasonCode;
  source: string;
  evaluatedAt: string;
  validUntil?: string | null;
  limits?: Record<string, number> | null;
}

export interface EvaluateEntitlementsRequest {
  requests: EntitlementRequest[];
}

export interface EvaluateEntitlementsResponse {
  decisions: EntitlementDecision[];
}

export interface EntitlementGrant {
  capability: string;
  source: string;
  limits?: Record<string, number> | null;
  validFrom?: string | null;
  validUntil?: string | null;
}

export interface EntitlementSnapshot {
  tenant: string;
  issuedAt: string;
  expiresAt: string;
  graceUntil: string;
  entitlements: EntitlementGrant[];
  /** Optional monotonic anti-rollback nonce per tenant+deployment (fabric#9). */
  counter?: number | null;
}

export interface SignedEntitlementSnapshot {
  keyId: string;
  algorithm: string;
  payloadJson: string;
  signature: string;
}

export interface ImportSignedEntitlementSnapshotRequest {
  document: SignedEntitlementSnapshot;
  validateOnly?: boolean;
}

export interface ImportSignedEntitlementSnapshotResponse {
  accepted: boolean;
  reasonCode: ReasonCode | string;
  snapshot?: EntitlementSnapshot | null;
}

export interface CapabilityDefinition {
  id: string;
  kind: TaxonomyKind;
  description: string;
  reserved?: boolean;
}

export interface DecisionReasonDefinition {
  code: string;
  description: string;
  deny?: boolean;
}

export interface TaxonomyCatalogDocument {
  contractVersion: string;
  capabilities: CapabilityDefinition[];
  reasons: DecisionReasonDefinition[];
}

export interface EntitlementBundleRequest {
  tenant: string;
  offer: EntitlementOffer;
  lifecycleState: EntitlementLifecycleState;
  issuedAt: string;
  expiresAt: string;
  graceUntil: string;
}

export interface EntitlementBundleResolution {
  snapshot: EntitlementSnapshot;
  resolutionReasonCode: ReasonCode | string;
}

export interface TenantLifecycleTimeline {
  trialStartedAt: string;
  trialExpiresAt: string;
  readOnlyUntil?: string | null;
  lockedAt?: string | null;
  retentionUntil?: string | null;
  purgedAt?: string | null;
}

export interface TenantLifecycleQuery {
  tenant: string;
  asOf?: string | null;
}

export interface TenantLifecycleStatus {
  tenant: string;
  state: TenantLifecycleState;
  reasonCode: ReasonCode | string;
  evaluatedAt: string;
  timeline: TenantLifecycleTimeline;
}

export interface TenantLifecycleTransitionRequest {
  tenant: string;
  transition: TenantLifecycleTransition;
  occurredAt: string;
  timeline: TenantLifecycleTimeline;
  phaseUntil?: string | null;
}

export interface TenantLifecycleTransitionResult {
  accepted: boolean;
  reasonCode: ReasonCode | string;
  lifecycle: TenantLifecycleStatus;
}

export interface DiscoveryEnrollmentTimeline {
  enrolledAt: string;
  credentialExpiresAt?: string | null;
  activatedAt?: string | null;
  lastRotatedAt?: string | null;
  suspendedAt?: string | null;
  revokedAt?: string | null;
}

export interface DiscoveryEnrollmentQuery {
  enrollmentId: string;
  tenant: TenantContext;
  principal: PrincipalContext;
  capability: CapabilityId;
  asOf?: string | null;
}

export interface DiscoveryEnrollmentStatus {
  enrollmentId: string;
  tenant: TenantContext;
  principal: PrincipalContext;
  capability: CapabilityId;
  state: DiscoveryEnrollmentState;
  reasonCode: ReasonCode | string;
  evaluatedAt: string;
  timeline: DiscoveryEnrollmentTimeline;
}

export interface DiscoveryEnrollmentTransitionRequest {
  enrollmentId: string;
  tenant: TenantContext;
  principal: PrincipalContext;
  capability: CapabilityId;
  transition: DiscoveryEnrollmentTransition;
  occurredAt: string;
  timeline: DiscoveryEnrollmentTimeline;
  credentialExpiresAt?: string | null;
}

export interface DiscoveryEnrollmentTransitionResult {
  accepted: boolean;
  reasonCode: ReasonCode | string;
  enrollment: DiscoveryEnrollmentStatus;
}

export interface DiscoveryLifecycleEvent {
  eventId: string;
  occurredAt: string;
  tenant: TenantContext;
  enrollmentId: string;
  principalId: string;
  source: string;
  eventType: DiscoveryLifecycleEventType;
  reasonCode: ReasonCode | string;
  capability: CapabilityId;
  correlationId: string;
  metadata?: Record<string, string> | null;
}

export type AuditCategory = "Data" | "Admin" | "Security";

export type AuditOutcome = "Success" | "Failure" | "Denied";

/** Redaction-safe projection of the acting principal. Never carries claims, secrets or tokens. */
export interface AuditActor {
  principalId: string;
  displayName?: string | null;
  roles?: string[];
}

export interface AuditResource {
  value: string;
  type?: string | null;
}

/** Append-only audit event envelope shared across VEV products (fabric#6). */
export interface AuditEvent {
  eventId: string;
  occurredAt: string;
  tenant: TenantContext;
  actor: AuditActor;
  source: string;
  action: string;
  resource: AuditResource;
  category: AuditCategory;
  outcome: AuditOutcome;
  correlationId: string;
  causationId?: string | null;
  /** Product-supplied, string-valued context. Must not carry secrets or customer content. */
  metadata?: Record<string, string> | null;
}

// Extensions (fabric#10) — the closed extension-type set + the deny-by-default manifest.
export type ExtensionType =
  | "provider-adapter"
  | "importer-exporter"
  | "policy-pack"
  | "workflow-action"
  | "connector"
  | "ui-extension"
  | "domain-module";

export interface ExtensionCompatibility {
  fabricApi: string;
  product?: string | null;
}

export interface ExtensionManifest {
  id: string;
  version: string;
  publisher: string;
  type: ExtensionType;
  compatibleWith: ExtensionCompatibility;
  /** Declared capabilities; deny-by-default. A reserved paid capability here is rejected. */
  permissions?: CapabilityId[];
  resources?: ResourceId[];
  network?: string[];
  secrets?: string[];
}

export interface ExtensionValidationError {
  code: string;
  message: string;
}

export interface ExtensionValidationResult {
  valid: boolean;
  errors: ExtensionValidationError[];
}

export const EXTENSION_VALIDATION_REASON_CODES = {
  missingId: "extension_missing_id",
  missingVersion: "extension_missing_version",
  missingPublisher: "extension_missing_publisher",
  missingFabricApiCompatibility: "extension_missing_fabric_api_compatibility",
  reservedCapabilityDeclared: "extension_reserved_capability"
} as const;

export const ATLAS_CAPABILITIES = {
  catalogueRead: "atlas.catalogue.read",
  catalogueWrite: "atlas.catalogue.write",
  analysisIntegrationMap: "atlas.analysis.integration-map",
  analysisEndOfLife: "atlas.analysis.eol",
  analysisApm: "atlas.analysis.apm",
  analysisRoadmap: "atlas.analysis.roadmap",
  aiReview: "atlas.ai.review",
  discoveryIngestion: "atlas.discovery.ingestion",
  portalReadonly: "atlas.portal.readonly",
  exportPortableBundle: "atlas.export.portable-bundle",
  exportArchiMate: "atlas.export.archimate"
} as const;

export const ATLAS_LIMIT_KEYS = {
  entities: "atlas.entities",
  users: "atlas.users",
  storage: "atlas.storage",
  workspaces: "atlas.workspaces",
  importJobs: "atlas.import.jobs",
  repositoryApplicationMax: "atlas.repository.application.max"
} as const;

export const FABRIC_CAPABILITIES = {
  marketplaceInstall: "fabric.marketplace.install"
} as const;

export const PORTIC_TAXONOMY = {
  governancePolicyAdvanced: "portic.governance.policy.advanced",
  gatewayProvidersMax: "portic.gateway.providers.max"
} as const;

export const DECISION_REASON_CODES = {
  allow: "allow",
  roleMissing: "role_missing",
  entitlementGranted: "entitlement_granted",
  entitlementDenied: "entitlement_denied",
  entitlementUnavailable: "entitlement_unavailable",
  entitlementSnapshotInvalid: "entitlement_snapshot_invalid",
  entitlementSnapshotStale: "entitlement_snapshot_stale",
  entitlementSnapshotTenantMismatch: "entitlement_snapshot_tenant_mismatch",
  lifecycleTrialExpired: "lifecycle_trial_expired",
  lifecycleReadOnly: "lifecycle_read_only",
  lifecycleLocked: "lifecycle_locked",
  lifecycleRetention: "lifecycle_retention",
  lifecyclePurged: "lifecycle_purged",
  lifecycleTransitionInvalid: "lifecycle_transition_invalid",
  discoveryEnrollmentPending: "discovery_enrollment_pending",
  discoveryEnrollmentSuspended: "discovery_enrollment_suspended",
  discoveryEnrollmentRevoked: "discovery_enrollment_revoked",
  discoveryCredentialExpired: "discovery_credential_expired",
  discoveryLifecycleTransitionInvalid: "discovery_lifecycle_transition_invalid",
  entitlementSnapshotRolledBack: "entitlement_snapshot_rolled_back",
  entitlementClockRegression: "entitlement_clock_regression",
  trialExpired: "trial_expired"
} as const;

export const DISCOVERY_AUDIT_VOCABULARY = {
  enrollmentCreateAction: "fabric.discovery.enrollment.create",
  enrollmentActivateAction: "fabric.discovery.enrollment.activate",
  credentialRotateAction: "fabric.discovery.credential.rotate",
  enrollmentSuspendAction: "fabric.discovery.enrollment.suspend",
  enrollmentRevokeAction: "fabric.discovery.enrollment.revoke",
  ingestionAcceptAction: "atlas.discovery.ingestion.accept",
  ingestionDenyAction: "atlas.discovery.ingestion.deny"
} as const;
