// Game entity - public API
// Pure types (interfaces, type aliases)
export type {
  Game,
  GameRef,
  GameId,
  GameUser,
  GamePrivacySettings,
  GameRecruitment,
  GamesQuery,
  CreateGameInput,
  CreateGamePrivacySettingsInput,
  Tag,
  Invitation,
  InvitationType,
  AttributeSchema,
  AttributeSpecification,
  AttributeValueSpecification,
  Character,
  CharacterId,
  CharacterAttribute,
  CharacterAttributeId,
  CharacterInput,
  CharacterAttributeInput,
  CharacterPrivacySettings,
  ApiCharacterStatus,
  PlayerCharacterInfo,
  Room,
  RoomId,
  RoomClaim,
  RoomAccess,
  RoomSettings,
  PendingPost,
  ChatRoom,
  ChatRoomAccess,
  CreateChatRoomInput,
  UpdateChatRoomInput,
  NotepadEntry,
  NotepadType,
  CreateNotepadEntryInput,
  UpdateNotepadEntryInput,
  CreateRoomInput,
  PostPendencyInput,
  CreatePostInput,
  DiceRollInput,
  Post,
  PostId,
  PostBbText,
  DiceRoll,
  PostReview,
  FirstUnreadPostResult,
  FirstUnreadCommentResult,
} from "./model/types";

// Enums (exported as values, can also be used as types)
export {
  GameStatus,
  GameRole,
  ClosedReason,
  CommentariesAccessMode,
  AttributeSchemaType,
  AttributeSpecificationType,
  Alignment,
  RoomType,
  RoomAccessType,
  RoomAccessPolicy,
  ReviewSign,
  GameStatusTransition,
  GamePremoderationTransition,
} from "./model/types";

// Store
export { useGamesStore, useGameDetailsStore } from "./model/store";
export { useRatedPostsStore } from "./model/ratedPostsStore";
export { usePulseStore, getWeekStartUtc } from "./model/pulseStore";
export type { PulseSearchParams } from "./model/pulseStore";

// Composables
export { useGameDisplay } from "./model/useGameDisplay";

// API
export { gameApi } from "./api";

// UI Components
export {
  GameStatusBadge,
  PostReviewItem,
  GameLink,
  RoomLink,
  CharacterCard,
  CharacterSheetFields,
} from "./ui";
