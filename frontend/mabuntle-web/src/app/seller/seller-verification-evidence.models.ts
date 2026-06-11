export type SellerVerificationEvidenceType =
  | 'BusinessRegistration'
  | 'IdentityOrRepresentative'
  | 'FulfilmentAddress'
  | 'BrandAuthorization'
  | 'ProductAuthenticity'
  | 'Other';

export interface SellerVerificationEvidenceResponse {
  evidenceId: string;
  evidenceType: SellerVerificationEvidenceType;
  originalFileName: string;
  contentType: string;
  byteSize: number;
  sha256Hash: string;
  note: string | null;
  uploadedAtUtc: string;
  removedAtUtc: string | null;
}
