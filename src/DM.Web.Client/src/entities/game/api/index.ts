// Game API
export { default as gameApi } from "./gameApi";

// Tag catalog administration
export { default as gameTagApi } from "./gameTagApi";
export type {
  ModerationTagGroup,
  ModerationTag,
  CreateTagGroupRequest,
  UpdateTagGroupRequest,
  CreateTagRequest,
  UpdateTagRequest,
} from "./gameTagApi";
