export const CONTRACT_VERSION = "1" as const;

export type ReasonCode =
  | "entitlement_granted"
  | "entitlement_denied"
  | "entitlement_unavailable"
  | "entitlement_snapshot_invalid"
  | "entitlement_snapshot_stale"
  | "entitlement_snapshot_tenant_mismatch";

export interface TenantContext {
  tenantId: string;
}

export interface PrincipalContext {
  principalId: string;
  displayName?: string | null;
  roles: string[];
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
