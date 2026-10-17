export interface ServerInstalledMod {
    uniqueId: string;
    name: string;
    version: string;
    author: string;
    description: string;
    isFramework: boolean;
}

export interface RequiredModRule {
    uniqueId: string;
    name: string;
    minVersion?: string;
    required: boolean;
}

export interface BlacklistedModRule {
    uniqueId: string;
    name: string;
    reason: string;
}

export interface ModPolicy {
    enforcementMode: "Disabled" | "RequireSmapi" | "StrictRequired";
    unknownModsMode?: "Ignore" | "StrictReject";
    whitelistModIds?: string[];
    requiredMods: RequiredModRule[];
    blacklistedMods: BlacklistedModRule[];
}

export interface CustomModItem {
    uniqueId: string;
    name: string;
    version: string;
    author?: string;
    description?: string;
    folderName: string;
    sizeBytes: number;
    downloadUrl?: string;
}

export interface PeerModItem {
    uniqueId: string;
    version: string;
}

export interface ConnectedPeerAudit {
    playerId: number;
    farmerName: string;
    hasSmapi: boolean;
    smapiVersion: string;
    modCount: number;
    connectedAtUtc: string;
    mods: PeerModItem[];
}

export interface ModRejectionLog {
    playerId: number;
    farmerName: string;
    reason: string;
    modId: string;
    timestampUtc: string;
}

export interface ModPolicyResponse {
    policy: ModPolicy;
    installedCount: number;
    rejections: ModRejectionLog[];
    connectedPeers: ConnectedPeerAudit[];
}
