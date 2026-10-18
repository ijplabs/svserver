export interface ServerStatus {
  inviteCode?: string | null;
  steamRelayReady: boolean;
  galaxyLobbyState?: string | null;
  steamSessionState: string;
  authReadiness?: string | null;
  connectionStatusCode: string;
  serverVersion: string;
  gameVersion: string;
  isOnline: boolean;
  isReady: boolean;
  dayTransitionComplete: boolean;
  lastUpdated: string;
  farmName: string;
  serverName: string;
  startedAtUtc?: string | null;
  day: number;
  season: 'spring' | 'summer' | 'fall' | 'winter' | string;
  year: number;
  timeOfDay: number;
  farmTypeKey: string;
  isPaused: boolean;
  isManuallyPaused?: boolean;
  pauseReason?: string;
  pausedBy?: string;
  pauseRemainingSeconds?: number;
  tps: number;
  sequence: number;
}

export interface PlayerInfo {
  id: number;
  name: string;
  isOnline: boolean;
  location: string;
  profession?: string;
}

export interface PlayersResponse {
  players: PlayerInfo[];
  sequence: number;
}

export interface CropTileState {
  x: number;
  y: number;
  location: string;
  cropId: string;
  harvestItemId: string;
  harvestItemName: string;
  currentPhase: number;
  totalPhases: number;
  daysUntilHarvest: number;
  isWatered: boolean;
  fertilizerId?: string | null;
  fertilizerName?: string | null;
  isReadyForHarvest: boolean;
  isDead: boolean;
  regrowDays: number;
  ownerId?: number;
  ownerName?: string;
}

export interface FarmCropsSnapshot {
  capturedAtUtc: string;
  totalCrops: number;
  readyCount: number;
  wateredCount: number;
  unwateredCount: number;
  deadCount: number;
  crops: CropTileState[];
}

export interface CabinInfo {
  tileX: number;
  tileY: number;
  isHidden: boolean;
  type: string;
  ownerId: number;
  ownerName: string;
  isAssigned: boolean;
}

export interface CabinsResponse {
  cabins: CabinInfo[];
  totalCabins: number;
  assignedCount: number;
  unassignedCount: number;
  activeStrategy: string;
}

export interface ItemInfo {
  id: string;
  name: string;
  description?: string;
  category?: string;
  price?: number;
  icon?: string;
}

export type CabinStrategyType = 'CabinStack' | 'FarmhouseStack' | 'None';

export interface NewGameParams {
  farmType?: number | string;
  farmName?: string;
  startingCabins?: number;
  cabinStrategy?: CabinStrategyType;
  maxPlayers?: number;
  allowIpConnections?: boolean;
  profitMargin?: number;
  separateWallets?: boolean;
  allowCabinRelocation?: boolean;
}

export interface NewGameResponse {
  success: boolean;
  message?: string;
  error?: string;
}

