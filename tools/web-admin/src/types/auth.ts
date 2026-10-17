export type UserRole = 'SUPERADMIN' | 'ADMIN' | 'PLAYER';

export interface WebSessionInfo {
  token: string;
  farmerId: number;
  username: string;
  role: UserRole;
  expiresIn?: number;
}

export interface LoginRequest {
  username: string;
  password: string;
}

export interface RegisterRequest {
  code: string;
  password: string;
}

export interface AuthMeResponse {
  authenticated: boolean;
  farmerId: number;
  username: string;
  role: UserRole;
}
