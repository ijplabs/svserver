export interface PendingPlayer {
  playerId: number;
  farmerName: string;
  failedAttempts: number;
  secondsInLobby: number;
  isNewPlayer: boolean;
}

export interface WebUser {
  farmerId: number;
  username: string;
  role: 'SUPERADMIN' | 'ADMIN' | 'PLAYER';
  createdAtUtc: string;
  lastLoginUtc?: string | null;
}

export interface AvailableFarmer {
  farmerId: number;
  farmerName: string;
  isOnline: boolean;
  hasWebAccount: boolean;
  role: string;
}

export interface CreateUserRequest {
  farmerId: number;
  username: string;
  password: string;
  role: 'ADMIN' | 'PLAYER';
}
