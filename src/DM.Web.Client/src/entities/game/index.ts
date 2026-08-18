// Game entity - public API
// Pure types (interfaces, type aliases)
export type {
  Game,
  GameRef,
  GameId,
  GameRecruitment,
  CreateGameInput,
  Tag,
  Invitation,
  AttributeSchema,
  AttributeSpecification,
  AttributeValueSpecification,
  Character,
  CharacterInput,
  CharacterPrivacySettings,
  CharacterStatusTransition,
  PlayerCharacterInfo,
  Room,
  RoomAccess,
  PostPendency,
  CreateRoomInput,
  DiceRollInput,
  Post,
  PostReview,
} from "./model/types";

// Enums (exported as values, can also be used as types)
export {
  GameStatus,
  GameParticipation,
  ClosedReason,
  CommentariesAccessMode,
  AttributeSchemaType,
  AttributeSpecificationType,
  RoomType,
  RoomAccessType,
  RoomAccessPolicy,
  GameStatusTransition,
  GamePremoderationTransition,
} from "./model/types";

// Store
export { useGamesStore } from "./model/store";
export { useGameDetailsStore } from "./model/detailsStore";
export { useRatedPostsStore } from "./model/ratedPostsStore";
export { usePulseStore } from "./model/pulseStore";
export { buildRatedPostsParams } from "./model/ratedPostsParams";
export type {
  PulseSearchParams,
  RatedPostsScope,
} from "./model/ratedPostsParams";

// Composables
export { useGameDisplay } from "./model/useGameDisplay";

// Lifecycle caption of a character, shared by the roster card and the
// character's own page.
export { characterStatusLabel } from "./model/characterStatus";

// Attribute-schema helpers: pure functions over the schema shape declared
// above, framework-free and shared by every embed site of the editor.
export {
  SPEC_TYPE_LABELS,
  SPEC_TYPE_OPTIONS,
  usesMaxLength,
  usesValues,
  usesModifier,
  isBbCode,
  createEmptySchema,
  createEmptySpec,
  cloneSpecsWithNewIds,
  cloneSchema,
  normalizeSpecForType,
} from "./model/schemaHelpers";

// API
export { gameApi, gameTagApi } from "./api";
export type { ModerationTagGroup, ModerationTag } from "./api";

// UI Components
export {
  GameStatusBadge,
  PostReviewItem,
  GameReviewCard,
  GameLink,
  RoomLink,
  CharacterCard,
  CharacterSheetFields,
} from "./ui";
