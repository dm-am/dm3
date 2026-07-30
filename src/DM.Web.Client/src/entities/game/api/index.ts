// Game API
export { default as gameApi } from "./gameApi";
export { default } from "./gameApi";

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
