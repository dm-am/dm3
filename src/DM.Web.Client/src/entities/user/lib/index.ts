export * from "./helpers";

// The name and the profile everything removed accounts left is signed with.
export {
  REMOVED_ACCOUNT_NAME,
  isRemovedAccountName,
  removedAccountRoute,
} from "./removedAccount";

// User-coupled composables (current-user store, roles, profile editing)
export { useMessagePermissions } from "./useMessagePermissions";
export type { MessagePermissions } from "./useMessagePermissions";
export { useProfileEdit } from "./useProfileEdit";

// Session lifecycle. The session STATE is shared/stores/auth; these are the
// account calls that move it.
export {
  register,
  signIn,
  completeSecondFactor,
  signOut,
  signOutAll,
  fetchUser,
  restoreSession,
} from "./session";
export type { SignInOutcome } from "./session";
