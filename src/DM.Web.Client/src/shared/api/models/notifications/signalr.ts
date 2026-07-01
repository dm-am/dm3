/**
 * SignalR notification types for real-time events
 */

/**
 * Event types matching backend EventType enum
 */
export enum EventType {
  Unknown = 0,

  // Community events (1-99)
  NewUser = 1,
  ActivatedUser = 2,
  UserAvatarChanged = 24,
  NewMessage = 11,
  ChangedMessage = 12,
  LikedMessage = 13,
  NewGlobalChatMessage = 31,
  LikedGlobalChatMessage = 32,
  NewPoll = 51,

  // Forum events (100-199)
  NewForumTopic = 101,
  ChangedForumTopic = 102,
  DeletedForumTopic = 103,
  LikedTopic = 104,
  NewForumComment = 111,
  ChangedForumComment = 112,
  DeletedForumComment = 113,
  LikedForumComment = 114,

  // Game events (300-499)
  NewGame = 301,
  ChangedGame = 302,
  DeletedGame = 303,
  StatusGameModeration = 321,
  StatusGameDraft = 322,
  StatusGameRequirement = 323,
  StatusGameActive = 324,
  StatusGameFrozen = 325,
  StatusGameFinished = 326,
  StatusGameClosed = 327,
  NewGameComment = 331,
  ChangedGameComment = 332,
  DeletedGameComment = 333,
  LikedGameComment = 334,
  AssignmentRequestCreated = 351,
  AssignmentRequestAccepted = 352,
  AssignmentRequestRejected = 353,
  PlayerInvitationCreated = 354,
  PlayerInvitationAccepted = 355,
  PlayerInvitationRejected = 356,
  ReaderInvitationCreated = 357,
  ReaderInvitationAccepted = 358,
  ReaderInvitationRejected = 359,
  NewCharacter = 361,
  ChangedCharacter = 362,
  DeletedCharacter = 363,
  StatusCharacterDeclined = 371,
  StatusCharacterAccepted = 372,
  StatusCharacterDied = 373,
  StatusCharacterResurrected = 374,
  StatusCharacterLeft = 375,
  StatusCharacterReturned = 376,
  NewRoom = 381,
  ChangedRoom = 382,
  DeletedRoom = 383,
  RoomPendingCreated = 384,
  RoomPendingResponded = 385,
  NewPost = 401,
  ChangedPost = 402,
  DeletedPost = 403,
  PostVoted = 411,
}

/**
 * SignalR notification payload
 */
export interface SignalRNotification {
  id: string;
  eventType: EventType;
  payload: Record<string, unknown>;
}

/**
 * Notification handler callback type
 */
export type NotificationHandler = (notification: SignalRNotification) => void;
