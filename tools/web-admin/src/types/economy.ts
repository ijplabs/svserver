export interface FarmerAccount {
  farmerId: number;
  farmerName: string;
  bankBalance: number;
  walletBalance: number;
  totalWealth: number;
  isOnline: boolean;
}

export interface EconomyOverview {
  useSeparateWallets: boolean;
  sharedFarmMoney: number;
  accounts: FarmerAccount[];
}
