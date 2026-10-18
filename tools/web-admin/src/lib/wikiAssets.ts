// Pure JS MD5 Implementation (RFC 1321) for generating MediaWiki asset paths
function rotateLeft(lValue: number, iShiftBits: number): number {
  return (lValue << iShiftBits) | (lValue >>> (32 - iShiftBits));
}

function addUnsigned(lX: number, lY: number): number {
  const lX4 = lX & 0x40000000;
  const lY4 = lY & 0x40000000;
  const lX8 = lX & 0x80000000;
  const lY8 = lY & 0x80000000;
  const lResult = (lX & 0x3fffffff) + (lY & 0x3fffffff);
  if (lX4 & lY4) return lResult ^ 0x80000000 ^ lX8 ^ lY8;
  if (lX4 | lY4) {
    if (lResult & 0x40000000) return lResult ^ 0xc0000000 ^ lX8 ^ lY8;
    else return lResult ^ 0x40000000 ^ lX8 ^ lY8;
  } else {
    return lResult ^ lX8 ^ lY8;
  }
}

function F(x: number, y: number, z: number) { return (x & y) | (~x & z); }
function G(x: number, y: number, z: number) { return (x & z) | (y & ~z); }
function H(x: number, y: number, z: number) { return x ^ y ^ z; }
function I(x: number, y: number, z: number) { return y ^ (x | ~z); }

function FF(a: number, b: number, c: number, d: number, x: number, s: number, ac: number) {
  a = addUnsigned(a, addUnsigned(addUnsigned(F(b, c, d), x), ac));
  return addUnsigned(rotateLeft(a, s), b);
}
function GG(a: number, b: number, c: number, d: number, x: number, s: number, ac: number) {
  a = addUnsigned(a, addUnsigned(addUnsigned(G(b, c, d), x), ac));
  return addUnsigned(rotateLeft(a, s), b);
}
function HH(a: number, b: number, c: number, d: number, x: number, s: number, ac: number) {
  a = addUnsigned(a, addUnsigned(addUnsigned(H(b, c, d), x), ac));
  return addUnsigned(rotateLeft(a, s), b);
}
function II(a: number, b: number, c: number, d: number, x: number, s: number, ac: number) {
  a = addUnsigned(a, addUnsigned(addUnsigned(I(b, c, d), x), ac));
  return addUnsigned(rotateLeft(a, s), b);
}

function convertToWordArray(string: string): number[] {
  let lWordCount;
  const lMessageLength = string.length;
  const lNumberOfWords_temp1 = lMessageLength + 8;
  const lNumberOfWords_temp2 = (lNumberOfWords_temp1 - (lNumberOfWords_temp1 % 64)) / 64;
  const lNumberOfWords = (lNumberOfWords_temp2 + 1) * 16;
  const lWordArray = Array(lNumberOfWords).fill(0);
  let lBytePosition = 0;
  let lByteCount = 0;
  while (lByteCount < lMessageLength) {
    lWordCount = (lByteCount - (lByteCount % 4)) / 4;
    lBytePosition = (lByteCount % 4) * 8;
    lWordArray[lWordCount] = lWordArray[lWordCount] | (string.charCodeAt(lByteCount) << lBytePosition);
    lByteCount++;
  }
  lWordCount = (lByteCount - (lByteCount % 4)) / 4;
  lBytePosition = (lByteCount % 4) * 8;
  lWordArray[lWordCount] = lWordArray[lWordCount] | (0x80 << lBytePosition);
  lWordArray[lNumberOfWords - 2] = lMessageLength << 3;
  lWordArray[lNumberOfWords - 1] = lMessageLength >>> 29;
  return lWordArray;
}

function wordToHex(lValue: number): string {
  let WordToHexValue = '';
  let WordToHexValue_temp = '';
  for (let lCount = 0; lCount <= 3; lCount++) {
    const lByte = (lValue >>> (lCount * 8)) & 255;
    WordToHexValue_temp = '0' + lByte.toString(16);
    WordToHexValue = WordToHexValue + WordToHexValue_temp.substring(WordToHexValue_temp.length - 2);
  }
  return WordToHexValue;
}

export function md5(string: string): string {
  const x = convertToWordArray(string);
  let a = 0x67452301;
  let b = 0xefcdab89;
  let c = 0x98badcfe;
  let d = 0x10325476;
  const S11 = 7, S12 = 12, S13 = 17, S14 = 22;
  const S21 = 5, S22 = 9, S23 = 14, S24 = 20;
  const S31 = 4, S32 = 11, S33 = 16, S34 = 23;
  const S41 = 6, S42 = 10, S43 = 15, S44 = 21;

  for (let k = 0; k < x.length; k += 16) {
    const AA = a; const BB = b; const CC = c; const DD = d;
    a = FF(a, b, c, d, x[k + 0], S11, 0xd76aa478);
    d = FF(d, a, b, c, x[k + 1], S12, 0xe8c7b756);
    c = FF(c, d, a, b, x[k + 2], S13, 0x242070db);
    b = FF(b, c, d, a, x[k + 3], S14, 0xc1bdceee);
    a = FF(a, b, c, d, x[k + 4], S11, 0xf57c0faf);
    d = FF(d, a, b, c, x[k + 5], S12, 0x4787c62a);
    c = FF(c, d, a, b, x[k + 6], S13, 0xa8304613);
    b = FF(b, c, d, a, x[k + 7], S14, 0xfd469501);
    a = FF(a, b, c, d, x[k + 8], S11, 0x698098d8);
    d = FF(d, a, b, c, x[k + 9], S12, 0x8b44f7af);
    c = FF(c, d, a, b, x[k + 10], S13, 0xffff5bb1);
    b = FF(b, c, d, a, x[k + 11], S14, 0x895cd7be);
    a = FF(a, b, c, d, x[k + 12], S11, 0x6b901122);
    d = FF(d, a, b, c, x[k + 13], S12, 0xfd987193);
    c = FF(c, d, a, b, x[k + 14], S13, 0xa679438e);
    b = FF(b, c, d, a, x[k + 15], S14, 0x49b40821);

    a = GG(a, b, c, d, x[k + 1], S21, 0xf61e2562);
    d = GG(d, a, b, c, x[k + 6], S22, 0xc040b340);
    c = GG(c, d, a, b, x[k + 11], S23, 0x265e5a51);
    b = GG(b, c, d, a, x[k + 0], S24, 0xe9b6c7aa);
    a = GG(a, b, c, d, x[k + 5], S21, 0xd62f105d);
    d = GG(d, a, b, c, x[k + 10], S22, 0x02441453);
    c = GG(c, d, a, b, x[k + 15], S23, 0xd8a1e681);
    b = GG(b, c, d, a, x[k + 4], S24, 0xe7d3fbc8);
    a = GG(a, b, c, d, x[k + 9], S21, 0x21e1cde6);
    d = GG(d, a, b, c, x[k + 14], S22, 0xc33707d6);
    c = GG(c, d, a, b, x[k + 3], S23, 0xf4d50d87);
    b = GG(b, c, d, a, x[k + 8], S24, 0x455a14ed);
    a = GG(a, b, c, d, x[k + 13], S21, 0xa9e3e905);
    d = GG(d, a, b, c, x[k + 2], S22, 0xfcefa3f8);
    c = GG(c, d, a, b, x[k + 7], S23, 0x676f02d9);
    b = GG(b, c, d, a, x[k + 12], S24, 0x8d2a4c8a);

    a = HH(a, b, c, d, x[k + 5], S31, 0xfffa3942);
    d = HH(d, a, b, c, x[k + 8], S32, 0x8771f681);
    c = HH(c, d, a, b, x[k + 11], S33, 0x6d9d6122);
    b = HH(b, c, d, a, x[k + 14], S34, 0xfde5380c);
    a = HH(a, b, c, d, x[k + 1], S31, 0xa4beea44);
    d = HH(d, a, b, c, x[k + 4], S32, 0x4bdecfa9);
    c = HH(c, d, a, b, x[k + 7], S33, 0xf6bb4b60);
    b = HH(b, c, d, a, x[k + 10], S34, 0xbebfbc70);
    a = HH(a, b, c, d, x[k + 13], S31, 0x289b7ec6);
    d = HH(d, a, b, c, x[k + 0], S32, 0xeaa127fa);
    c = HH(c, d, a, b, x[k + 3], S33, 0xd4ef3085);
    b = HH(b, c, d, a, x[k + 6], S34, 0x04881d05);
    a = HH(a, b, c, d, x[k + 9], S31, 0xd9d4d039);
    d = HH(d, a, b, c, x[k + 12], S32, 0xe6db99e5);
    c = HH(c, d, a, b, x[k + 15], S33, 0x1fa27cf8);
    b = HH(b, c, d, a, x[k + 2], S34, 0xc4ac5665);

    a = II(a, b, c, d, x[k + 0], S41, 0xf4292244);
    d = II(d, a, b, c, x[k + 7], S42, 0x432aff97);
    c = II(c, d, a, b, x[k + 14], S43, 0xab9423a7);
    b = II(b, c, d, a, x[k + 5], S44, 0xfc93a039);
    a = II(a, b, c, d, x[k + 12], S41, 0x655b59c3);
    d = II(d, a, b, c, x[k + 3], S42, 0x8f0ccc92);
    c = II(c, d, a, b, x[k + 10], S43, 0xffeff47d);
    b = II(b, c, d, a, x[k + 1], S44, 0x85845dd1);
    a = II(a, b, c, d, x[k + 8], S41, 0x6fa87e4f);
    d = II(d, a, b, c, x[k + 15], S42, 0xfe2ce6e0);
    c = II(c, d, a, b, x[k + 6], S43, 0xa3014314);
    b = II(b, c, d, a, x[k + 13], S44, 0x4e0811a1);
    a = II(a, b, c, d, x[k + 4], S41, 0xf7537e82);
    d = II(d, a, b, c, x[k + 11], S42, 0xbd3af235);
    c = II(c, d, a, b, x[k + 2], S43, 0x2ad7d2bb);
    b = II(b, c, d, a, x[k + 9], S44, 0xeb86d391);

    a = addUnsigned(a, AA);
    b = addUnsigned(b, BB);
    c = addUnsigned(c, CC);
    d = addUnsigned(d, DD);
  }
  return (wordToHex(a) + wordToHex(b) + wordToHex(c) + wordToHex(d)).toLowerCase();
}

/**
 * Builds the official Stardew Valley Wiki URL for a given image filename.
 * Follows MediaWiki MD5 hash path: /images/{firstChar}/{firstTwoChars}/{filename}
 */
export function getStardewWikiImageUrl(filename: string): string {
  if (!filename) return '';
  // MediaWiki converts spaces to underscores
  const cleanName = filename.trim().replace(/ /g, '_');
  const hash = md5(cleanName);
  const firstChar = hash[0];
  const firstTwo = hash.substring(0, 2);
  return `https://stardewvalleywiki.com/mediawiki/images/${firstChar}/${firstTwo}/${encodeURIComponent(cleanName)}`;
}

// Map of NPC internal / game names to their canonical Stardew Valley Wiki portrait image names
const NPC_PORTRAIT_MAP: Record<string, string> = {
  Wizard: 'Wizard.png',
  'M. Rasmodius': 'Wizard.png',
  Rasmodius: 'Wizard.png',
  'Mr. Qi': 'Mr._Qi.png',
  Qi: 'Mr._Qi.png',
  'Mr Qi': 'Mr._Qi.png',
  ProfessorSnail: 'Professor_Snail.png',
  'Professor Snail': 'Professor_Snail.png',
  Dwarf: 'Dwarf.png',
  Krobus: 'Krobus.png',
  Sandy: 'Sandy.png',
  Gunther: 'Gunther.png',
  Marlon: 'Marlon.png',
  Leo: 'Leo.png',
  Birdie: 'Birdie.png',
  Governor: 'Governor.png',
  Bouncer: 'Bouncer.png',
  Grandpa: 'Grandpa.png',
  Henchman: 'Henchman.png',
  Morris: 'Morris.png',
  Gourmand: 'Gourmand_Frog.png',
  'Gourmand Frog': 'Gourmand_Frog.png',
  Abigail: 'Abigail.png',
  Alex: 'Alex.png',
  Caroline: 'Caroline.png',
  Clint: 'Clint.png',
  Demetrius: 'Demetrius.png',
  Elliott: 'Elliott.png',
  Emily: 'Emily.png',
  Evelyn: 'Evelyn.png',
  George: 'George.png',
  Gus: 'Gus.png',
  Haley: 'Haley.png',
  Harvey: 'Harvey.png',
  Jas: 'Jas.png',
  Jodi: 'Jodi.png',
  Kent: 'Kent.png',
  Leah: 'Leah.png',
  Lewis: 'Lewis.png',
  Linus: 'Linus.png',
  Marnie: 'Marnie.png',
  Maru: 'Maru.png',
  Pam: 'Pam.png',
  Penny: 'Penny.png',
  Pierre: 'Pierre.png',
  Robin: 'Robin.png',
  Sam: 'Sam.png',
  Sebastian: 'Sebastian.png',
  Shane: 'Shane.png',
  Vincent: 'Vincent.png',
  Willy: 'Willy.png',
};

/**
 * Returns the wiki image URL for an NPC portrait.
 */
export function getNpcPortraitUrl(npcName: string): string {
  const mapped = NPC_PORTRAIT_MAP[npcName] || `${npcName.replace(/ /g, '_')}.png`;
  return getStardewWikiImageUrl(mapped);
}

// Map of Community Center Rooms to wiki image files
const ROOM_MAP: Record<string, string> = {
  Pantry: 'Pantry.png',
  'Crafts Room': 'Crafts_Room.png',
  Crafts: 'Crafts_Room.png',
  'Fish Tank': 'Fish_Tank.png',
  'Boiler Room': 'Boiler_Room.png',
  Boiler: 'Boiler_Room.png',
  Vault: 'Vault.png',
  'Bulletin Board': 'Bulletin_Board.png',
  Bulletin: 'Bulletin_Board.png',
  'Abandoned JojaMart': 'Abandoned_JojaMart.png',
  'Missing Bundle': 'Abandoned_JojaMart.png',
};

export function getRoomImageUrl(roomName: string): string {
  const mapped = ROOM_MAP[roomName] || `${roomName.replace(/ /g, '_')}.png`;
  return getStardewWikiImageUrl(mapped);
}

// Map of Bundle Names to wiki image files
const BUNDLE_MAP: Record<string, string> = {
  'Spring Crops': 'Spring_Crops_Bundle.png',
  'Summer Crops': 'Summer_Crops_Bundle.png',
  'Fall Crops': 'Fall_Crops_Bundle.png',
  'Quality Crops': 'Quality_Crops_Bundle.png',
  Animal: 'Animal_Bundle.png',
  Artisan: 'Artisan_Bundle.png',
  'Spring Foraging': 'Spring_Foraging_Bundle.png',
  'Summer Foraging': 'Summer_Foraging_Bundle.png',
  'Fall Foraging': 'Fall_Foraging_Bundle.png',
  'Winter Foraging': 'Winter_Foraging_Bundle.png',
  Construction: 'Construction_Bundle.png',
  'Exotic Foraging': 'Exotic_Foraging_Bundle.png',
  'River Fish': 'River_Fish_Bundle.png',
  'Lake Fish': 'Lake_Fish_Bundle.png',
  'Ocean Fish': 'Ocean_Fish_Bundle.png',
  'Night Fishing': 'Night_Fishing_Bundle.png',
  'Crab Pot': 'Crab_Pot_Bundle.png',
  'Specialty Fish': 'Specialty_Fish_Bundle.png',
  "Blacksmith's": "Blacksmith's_Bundle.png",
  "Geologist's": "Geologist's_Bundle.png",
  "Adventurer's": "Adventurer's_Bundle.png",
  "Chef's": "Chef's_Bundle.png",
  Dye: 'Dye_Bundle.png',
  'Field Research': 'Field_Research_Bundle.png',
  Fodder: 'Fodder_Bundle.png',
  "Enchanter's": "Enchanter's_Bundle.png",
  '2,500g': '2,500_Bundle.png',
  '2,500': '2,500_Bundle.png',
  '5,000g': '5,000_Bundle.png',
  '5,000': '5,000_Bundle.png',
  '10,000g': '10,000_Bundle.png',
  '10,000': '10,000_Bundle.png',
  '25,000g': '25,000_Bundle.png',
  '25,000': '25,000_Bundle.png',
  'The Missing': 'The_Missing_Bundle.png',
  'The Missing Bundle': 'The_Missing_Bundle.png',
  'Missing Bundle': 'The_Missing_Bundle.png',
  "Children's": "Children's_Bundle.png",
  "Forager's": "Forager's_Bundle.png",
  "Home Cook's": "Home_Cook's_Bundle.png",
};

export function getBundleImageUrl(bundleName: string): string {
  // If exact match
  if (BUNDLE_MAP[bundleName]) {
    return getStardewWikiImageUrl(BUNDLE_MAP[bundleName]);
  }
  // If already ends with Bundle
  const normalized = bundleName.endsWith('Bundle') ? bundleName : `${bundleName} Bundle`;
  return getStardewWikiImageUrl(`${normalized.replace(/ /g, '_')}.png`);
}

// Map of Joja Projects to wiki image files
const JOJA_PROJECT_MAP: Record<string, string> = {
  Greenhouse: 'Greenhouse.png',
  Minecarts: 'Minecart.png',
  Bridge: 'Quarry.png',
  Bus: 'Bus.png',
  Panning: 'Copper_Pan.png',
  'Movie Theater': 'Movie_Theater.png',
};

export function getJojaProjectImageUrl(projectKeyOrName: string): string {
  const mapped = JOJA_PROJECT_MAP[projectKeyOrName] || `${projectKeyOrName.replace(/ /g, '_')}.png`;
  return getStardewWikiImageUrl(mapped);
}

// Item name normalizer for Wiki image paths
const ITEM_MAP: Record<string, string> = {
  'Large Egg (White)': 'Large_White_Egg.png',
  'Large Egg (Brown)': 'Large_Brown_Egg.png',
  'Large White Egg': 'Large_White_Egg.png',
  'Large Brown Egg': 'Large_Brown_Egg.png',
  'Large Goat Milk': 'Large_Goat_Milk.png',
  'Refined Quartz': 'Refined_Quartz.png',
  'Earth Crystal': 'Earth_Crystal.png',
  'Frozen Tear': 'Frozen_Tear.png',
  'Fire Quartz': 'Fire_Quartz.png',
  'Solar Essence': 'Solar_Essence.png',
  'Void Essence': 'Void_Essence.png',
  'Bat Wing': 'Bat_Wing.png',
  'Wild Horseradish': 'Wild_Horseradish.png',
  'Cave Carrot': 'Cave_Carrot.png',
  'Hot Pepper': 'Hot_Pepper.png',
  'Green Bean': 'Green_Bean.png',
  'Red Cabbage': 'Red_Cabbage.png',
  'Sea Urchin': 'Sea_Urchin.png',
  'Duck Feather': 'Duck_Feather.png',
  'Nautilus Shell': 'Nautilus_Shell.png',
  'Fiddlehead Fern': 'Fiddlehead_Fern.png',
  'Fried Egg': 'Fried_Egg.png',
  'Maki Roll': 'Maki_Roll.png',
  'Tiger Trout': 'Tiger_Trout.png',
  'Largemouth Bass': 'Largemouth_Bass.png',
  'Sea Cucumber': 'Sea_Cucumber.png',
  'Super Cucumber': 'Super_Cucumber.png',
  'Rainbow Trout': 'Rainbow_Trout.png',
  'Red Snapper': 'Red_Snapper.png',
  'Crab Pot': 'Crab_Pot.png',
  'Copper Bar': 'Copper_Bar.png',
  'Iron Bar': 'Iron_Bar.png',
  'Gold Bar': 'Gold_Bar.png',
  'Iridium Bar': 'Iridium_Bar.png',
  'Axe': 'Axe.png',
  'Hoe': 'Hoe.png',
  'Pickaxe': 'Pickaxe.png',
  'Watering Can': 'Watering_Can.png',
  'Scythe': 'Scythe.png',
  'Copper Axe': 'Copper_Axe.png',
  'Steel Axe': 'Steel_Axe.png',
  'Gold Axe': 'Gold_Axe.png',
  'Iridium Axe': 'Iridium_Axe.png',
  'Copper Hoe': 'Copper_Hoe.png',
  'Steel Hoe': 'Steel_Hoe.png',
  'Gold Hoe': 'Gold_Hoe.png',
  'Iridium Hoe': 'Iridium_Hoe.png',
  'Copper Pickaxe': 'Copper_Pickaxe.png',
  'Steel Pickaxe': 'Steel_Pickaxe.png',
  'Gold Pickaxe': 'Gold_Pickaxe.png',
  'Iridium Pickaxe': 'Iridium_Pickaxe.png',
  'Copper Watering Can': 'Copper_Watering_Can.png',
  'Steel Watering Can': 'Steel_Watering_Can.png',
  'Gold Watering Can': 'Gold_Watering_Can.png',
  'Iridium Watering Can': 'Iridium_Watering_Can.png',
  'Golden Scythe': 'Golden_Scythe.png',
  'Iridium Scythe': 'Iridium_Scythe.png',
  'Trash Can': 'Trash_Can.png',
  'Fishing Rod': 'Bamboo_Pole.png',
  'Bamboo Pole': 'Bamboo_Pole.png',
  'Training Rod': 'Training_Rod.png',
  'Fiberglass Rod': 'Fiberglass_Rod.png',
  'Iridium Rod': 'Iridium_Rod.png',
  'Parsnip Seeds': 'Parsnip_Seeds.png',
  'Parsnip': 'Parsnip.png',
  'Potato Seeds': 'Potato_Seeds.png',
  'Potato': 'Potato.png',
  'Cauliflower Seeds': 'Cauliflower_Seeds.png',
  'Cauliflower': 'Cauliflower.png',
  'Bean Starter': 'Bean_Starter.png',
  'Kale Seeds': 'Kale_Seeds.png',
  'Kale': 'Kale.png',
  'Strawberry Seeds': 'Strawberry_Seeds.png',
  'Strawberry': 'Strawberry.png',
  'Mixed Seeds': 'Mixed_Seeds.png',
  'Corn Seeds': 'Corn_Seeds.png',
  'Corn': 'Corn.png',
  'Blueberry Seeds': 'Blueberry_Seeds.png',
  'Blueberry': 'Blueberry.png',
  'Melon Seeds': 'Melon_Seeds.png',
  'Melon': 'Melon.png',
  'Tomato Seeds': 'Tomato_Seeds.png',
  'Tomato': 'Tomato.png',
  'Pumpkin Seeds': 'Pumpkin_Seeds.png',
  'Pumpkin': 'Pumpkin.png',
  'Eggplant Seeds': 'Eggplant_Seeds.png',
  'Eggplant': 'Eggplant.png',
  'Yam Seeds': 'Yam_Seeds.png',
  'Yam': 'Yam.png',
  'Cranberry Seeds': 'Cranberry_Seeds.png',
  'Cranberries': 'Cranberries.png',
  'Wheat Seeds': 'Wheat_Seeds.png',
  'Wheat': 'Wheat.png',
  'Starfruit Seeds': 'Starfruit_Seeds.png',
  'Starfruit': 'Starfruit.png',
  'Ancient Seeds': 'Ancient_Seeds.png',
  'Ancient Fruit': 'Ancient_Fruit.png',
  'Wood': 'Wood.png',
  'Hardwood': 'Hardwood.png',
  'Stone': 'Stone.png',
  'Clay': 'Clay.png',
  'Fiber': 'Fiber.png',
  'Coal': 'Coal.png',
  'Copper Ore': 'Copper_Ore.png',
  'Iron Ore': 'Iron_Ore.png',
  'Gold Ore': 'Gold_Ore.png',
  'Iridium Ore': 'Iridium_Ore.png',
  'Work Boots': 'Work_Boots.png',
  'Sneakers': 'Sneakers.png',
  'Rubber Boots': 'Rubber_Boots.png',
  'Leather Boots': 'Leather_Boots.png',
  'Combat Boots': 'Combat_Boots.png',
  'Space Boots': 'Space_Boots.png',
  'Tundra Boots': 'Tundra_Boots.png',
  'Thermal Boots': 'Thermal_Boots.png',
  'Cowboy Boots': 'Cowboy_Boots.png',
  'Cinderclown Boots': 'Cinderclown_Boots.png',
  'Genie Shoes': 'Genie_Shoes.png',
  'Mermaid Boots': 'Mermaid_Boots.png',
  'Dragonscale Boots': 'Dragonscale_Boots.png',
  'Small Glow Ring': 'Small_Glow_Ring.png',
  'Glow Ring': 'Glow_Ring.png',
  'Small Magnet Ring': 'Small_Magnet_Ring.png',
  'Magnet Ring': 'Magnet_Ring.png',
  'Iridium Band': 'Iridium_Band.png',
  'Glowstone Ring': 'Glowstone_Ring.png',
  'Ruby Ring': 'Ruby_Ring.png',
  'Emerald Ring': 'Emerald_Ring.png',
  'Topaz Ring': 'Topaz_Ring.png',
  'Amethyst Ring': 'Amethyst_Ring.png',
  'Aquamarine Ring': 'Aquamarine_Ring.png',
  'Jade Ring': 'Jade_Ring.png',
  'Crabshell Ring': 'Crabshell_Ring.png',
  'Burglar\'s Ring': "Burglar's_Ring.png",
  'Savage Ring': 'Savage_Ring.png',
  'Slime Charmer Ring': 'Slime_Charmer_Ring.png',
  'Lucky Ring': 'Lucky_Ring.png',
  'Hot Java Ring': 'Hot_Java_Ring.png',
  'Soul Sapper Ring': 'Soul_Sapper_Ring.png',
  'Phoenix Ring': 'Phoenix_Ring.png',
  'Immunity Band': 'Immunity_Band.png',
  'Thorns Ring': 'Thorns_Ring.png',
  'Warrior Ring': 'Warrior_Ring.png',
  'Vampire Ring': 'Vampire_Ring.png',
  'Ring of Yoba': 'Ring_of_Yoba.png',
  'Jukebox Ring': 'Jukebox_Ring.png',
  'Rusty Sword': 'Rusty_Sword.png',
  'Wooden Blade': 'Wooden_Blade.png',
  'Silver Saber': 'Silver_Saber.png',
  'Iron Edge': 'Iron_Edge.png',
  'Galaxy Sword': 'Galaxy_Sword.png',
  'Infinity Blade': 'Infinity_Blade.png',
  'Lava Katana': 'Lava_Katana.png',
  'Claymore': 'Claymore.png',
  'Steel Smallsword': 'Steel_Smallsword.png',
  'Pirate\'s Sword': "Pirate's_Sword.png",
  'Cutlass': 'Cutlass.png',
  'Insect Head': 'Insect_Head.png',
  'Neptune\'s Glaive': "Neptune's_Glaive.png",
  'Templar\'s Blade': "Templar's_Blade.png",
  'Bone Sword': 'Bone_Sword.png',
  'Obsidian Edge': 'Obsidian_Edge.png',
};

export function getItemImageUrl(itemName: string): string {
  if (!itemName || itemName.toLowerCase() === 'unknown crop' || itemName.toLowerCase() === 'unknown') {
    return '';
  }

  // Clean prefixes like "(O)", "(W)", "(B)", "(BC)", "(Q)" and quality markers in parentheses
  let clean = itemName
    .replace(/^\([A-Za-z0-9_]+\)\s*/, '')
    .replace(/^\[[A-Za-z0-9_]+\]\s*/, '')
    .replace(/\s*\((Gold|Silver|Iridium)\)\s*/i, '')
    .trim();

  if (ITEM_MAP[clean]) {
    return getStardewWikiImageUrl(ITEM_MAP[clean]);
  }

  // Fallback to Item_Name.png
  return getStardewWikiImageUrl(`${clean.replace(/ /g, '_')}.png`);
}
