export interface PlayerMapPin {
  id: number;
  name: string;
  location: string;
  zone: 'farm' | 'town' | 'beach' | 'mountain' | 'forest' | 'mines' | 'desert' | 'island' | 'other';
  tileX: number;
  tileY: number;
  pixelX: number;
  pixelY: number;
  facingDirection: number;
  isOnline: boolean;
}

export interface FarmBuildingPin {
  name: string;
  type: string;
  tileX: number;
  tileY: number;
  tilesWide: number;
  tilesHigh: number;
  isCabin: boolean;
  ownerName?: string | null;
  ownerId?: number | null;
  isLobby: boolean;
  animalCount: number;
}

export interface MapTrackingResponse {
  farmType: string;
  farmWidth: number;
  farmHeight: number;
  players: PlayerMapPin[];
  buildings: FarmBuildingPin[];
  cropCountsByLocation: Record<string, number>;
}
