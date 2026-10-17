export interface VillagerFriendship {
  name: string;
  displayName: string;
  points: number;
  hearts: number;
  maxHearts: number;
  giftsThisWeek: number;
  maxGiftsPerWeek: number;
  giftGivenToday: boolean;
  talkedToToday: boolean;
  status: 'Friendly' | 'Dating' | 'Engaged' | 'Married' | 'Divorced' | string;
  birthdaySeason: string;
  birthdayDay: number;
  isBirthdayToday: boolean;
  isDatable: boolean;
  gender: 'Male' | 'Female' | 'Undefined' | string;
}

export interface FarmerSocialResponse {
  farmerId: number;
  farmerName: string;
  totalVillagersMet: number;
  maxHeartsAchieved: number;
  villagers: VillagerFriendship[];
}
