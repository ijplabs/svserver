export interface CharacterSkill {
  level: number;
  experience: number;
}

export interface InventoryItem {
  slotIndex: number;
  itemId: string;
  name: string;
  displayName: string;
  stack: number;
  quality: number; // 0 = normal, 1 = silver, 2 = gold, 4 = iridium
  categoryName: string;
}

export interface EquipmentItem {
  slot: string;
  itemId: string;
  name: string;
}

export interface FarmerProfile {
  id: number;
  name: string;
  farmName: string;
  favoriteThing: string;
  isMale: boolean;
  money: number;
  clubCoins: number;
  qiGems: number;
  health: number;
  maxHealth: number;
  stamina: number;
  maxStamina: number;
  currentLocation: string;
  tileX: number;
  tileY: number;
  skills: Record<string, CharacterSkill>;
  inventory: InventoryItem[];
  equipment: EquipmentItem[];
}
