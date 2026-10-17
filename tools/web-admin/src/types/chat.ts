export interface ChatHistoryEntry {
  id: string;
  author: string;
  message: string;
  source: 'in-game' | 'web' | 'system';
  timestampUtc: string;
}

export interface ChatSendResponse {
  success: boolean;
  author: string;
  message: string;
  timestamp: string;
}
