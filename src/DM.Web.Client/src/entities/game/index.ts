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
  GameParticipation,
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

// Attribute-schema helpers: pure functions over the schema shape declared
// above, framework-free and shared by every embed site of the editor.
export {
  SPEC_TYPE_LABELS,
  SPEC_TYPE_OPTIONS,
  usesMaxLength,
  usesValues,
  usesModifier,
  isBbCode,
  newSpecId,
  createEmptySchema,
  createEmptySpec,
  cloneSpecsWithNewIds,
  cloneSchema,
  normalizeSpecForType,
} from "./model/schemaHelpers";

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
