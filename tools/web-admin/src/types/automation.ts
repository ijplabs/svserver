export interface SleepVoteStatus {
  enabled: boolean;
  thresholdPercent: number;
  activeVotes: number;
  requiredVotes: number;
  eligiblePlayers: number;
  votePassed: boolean;
  isNight: boolean;
}

export interface TrackedAfkPlayer {
  farmerId: number;
  farmerName: string;
  idleMinutes: number;
  warningSent: boolean;
  location: string;
}

export interface AfkWatchdogStatus {
  enabled: boolean;
  warningMinutes: number;
  timeoutMinutes: number;
  nightOnly: boolean;
  trackedPlayers: TrackedAfkPlayer[];
}

export interface AutomationStatus {
  sleepVote?: SleepVoteStatus;
  afkWatchdog?: AfkWatchdogStatus;
}
