export type AuthRole =
  'Buyer' |
  'Seller' |
  'Admin' |
  'SuperAdmin' |
  'SupportAgent' |
  'FinanceOperator' |
  'FinanceApprover';

export interface AuthTokens {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
}

export interface AuthUser {
  userId: string;
  email: string;
  roles: AuthRole[];
}

export interface AuthState {
  currentUser: AuthUser | null;
  tokens: AuthTokens | null;
  initialized: boolean;
  loading: boolean;
}

export interface RegisterRequest {
  email: string;
  password: string;
  role: 'Buyer' | 'Seller';
}

export interface RegisterResponse {
  userId: string;
  email: string;
  role: 'Buyer' | 'Seller';
  sellerVerificationStatus: string | null;
  emailVerificationRequired: boolean;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface AuthResponse extends AuthUser, AuthTokens {}

export interface CurrentUserResponse extends AuthUser {}
