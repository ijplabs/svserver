export interface BackupEntry {
  fileName: string;
  sizeBytes: number;
  createdAtUtc: string;
  season?: string;
  day?: number;
  year?: number;
}
