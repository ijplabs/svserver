export interface FarmAnimalInfo {
  name: string;
  type: string;
  age: number;
  friendship: number;
  hearts: number;
  wasPet: boolean;
  wasFed: boolean;
  currentProduce?: string | null;
  location: string;
  homeBuilding: string;
}
