export interface PendingPlayer {
  playerId: number;
  playerIdStr?: string;
  farmerName: string;
  failedAttempts: number;
  secondsInLobby: number;
  isNewPlayer: boolean;
}

export interface WebUser {
  farmerId: number;
  farmerIdStr?: string;
  username: string;
  role: 'SUPERADMIN' | 'ADMIN' | 'PLAYER';
  createdAtUtc: string;
  lastLoginUtc?: string | null;
}

export interface AvailableFarmer {
  farmerId: number;
  farmerIdStr?: string;
  farmerName: string;
  isOnline: boolean;
  hasWebAccount: boolean;
  role: string;
}

export interface CreateUserRequest {
  farmerId: number | string;
  farmerIdStr?: string;
  username: string;
  password: string;
  role: 'ADMIN' | 'PLAYER';
}
