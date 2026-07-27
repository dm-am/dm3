export { default as MessageSearchPanel } from "./ui/MessageSearchPanel.vue";
export {
  useMessageSearchStore,
  scopeToIn,
  sortToParam,
  scopeNeedsTarget,
} from "./model/searchStore";
export type {
  MessageSearchResult,
  MessageSearchSourceType,
  SearchScope,
  SearchScopeKind,
  SearchSort,
} from "./model/types";
