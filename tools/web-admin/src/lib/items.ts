import cropsData from '../data/items/crops.json';
import fertilizerData from '../data/items/fertilizer.json';

interface ItemEntry {
  name: string;
  url: string;
}

const typedCrops = cropsData as Record<string, ItemEntry>;
const typedFertilizers = fertilizerData as Record<string, ItemEntry>;

function normalizeKey(str: string): string {
  return str
    .toLowerCase()
    .replace(/[^a-z0-9]/g, '_')
    .replace(/_+/g, '_')
    .replace(/^_|_$/g, '');
}

export function getCropImage(cropNameOrId: string): string | null {
  if (!cropNameOrId) return null;
  const key = normalizeKey(cropNameOrId);
  if (typedCrops[key]?.url) {
    return typedCrops[key].url;
  }
  // Try direct match
  const found = Object.values(typedCrops).find(
    (c) => c.name.toLowerCase() === cropNameOrId.toLowerCase()
  );
  return found ? found.url : null;
}

export function getFertilizerImage(fertNameOrId: string): string | null {
  if (!fertNameOrId) return null;
  const key = normalizeKey(fertNameOrId);
  if (typedFertilizers[key]?.url) {
    return typedFertilizers[key].url;
  }
  const found = Object.values(typedFertilizers).find(
    (f) => f.name.toLowerCase() === fertNameOrId.toLowerCase()
  );
  return found ? found.url : null;
}
