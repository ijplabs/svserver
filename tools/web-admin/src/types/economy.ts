export interface FarmerAccount {
  farmerId: number;
  farmerName: string;
  bankBalance: number;
  walletBalance: number;
  totalWealth: number;
  isOnline: boolean;
}

export interface VillageTarget {
  name: string;
  cost: number;
  progress: number;
}

export interface EconomyOverview {
  useSeparateWallets: boolean;
  sharedFarmMoney: number;
  villageTreasury?: number;
  villageTarget?: VillageTarget;
  accounts: FarmerAccount[];
}
