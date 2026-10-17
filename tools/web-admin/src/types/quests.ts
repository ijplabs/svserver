export interface QuestObjective {
  description: string;
  currentCount: number;
  maxCount: number;
  isComplete: boolean;
}

export interface Quest {
  id: string;
  title: string;
  description: string;
  currentObjective: string;
  rewardMoney: number;
  daysLeft: number;
  isDailyQuest: boolean;
  isComplete: boolean;
  questType: string;
}

export interface SpecialOrder {
  key: string;
  name: string;
  description: string;
  orderType: string;
  requester: string;
  daysLeft: number;
  objectives: QuestObjective[];
  isComplete: boolean;
}

export interface FarmerQuestsResponse {
  farmerId: number;
  farmerName: string;
  activeQuests: Quest[];
  specialOrders: SpecialOrder[];
}

export interface BundleIngredient {
  itemId: string;
  name: string;
  stack: number;
  quality: number; // 0 = normal, 1 = silver, 2 = gold, 4 = iridium
  isFulfilled: boolean;
}

export interface Bundle {
  bundleIndex: number;
  name: string;
  roomName: string;
  roomIndex: number;
  requiredItemsCount: number;
  fulfilledCount: number;
  isComplete: boolean;
  rewardName: string;
  rewardCount: number;
  ingredients: BundleIngredient[];
}

export interface CommunityRoom {
  roomIndex: number;
  name: string;
  displayName: string;
  reward: string;
  isComplete: boolean;
  totalBundles: number;
  completedBundles: number;
  bundles: Bundle[];
}

export interface JojaProject {
  key: string;
  name: string;
  description: string;
  cost: number;
  isComplete: boolean;
}

export interface CommunityCenterResponse {
  isComplete: boolean;
  isJojaRoute: boolean;
  totalBundles: number;
  completedBundles: number;
  rooms: CommunityRoom[];
  jojaProjects: JojaProject[];
}
