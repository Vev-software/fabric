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
  | "lifecycle_purged";

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
  lifecyclePurged: "lifecycle_purged"
} as const;
