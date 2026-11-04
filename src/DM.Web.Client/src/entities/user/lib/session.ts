import type {
  LoginCredentials,
  LoginResponse,
} from "@/shared/api/models/account";
import type { GeneralError, User } from "@/shared/api/models/common";
import type { UserSettings } from "@/shared/api/models/personal";
import type { RequestOptions } from "@/shared/api";
import { useAuthStore } from "@/shared/stores";
import { notifyFailure } from "@/shared/lib/errors";
import accountApi from "../api/accountApi";
import personalApi from "../api/personalApi";

// The calls that move the session; the store in shared keeps the state and stays domain-free.

// The theme and the page size of the account ride on the viewer: the user DTO
// carries no settings, the preferences do. The lists read the page size there,
// and the ui store offers the theme to a device that never chose one.
function withSettings(
  viewer: User,
  settings: Partial<UserSettings> | null | undefined,
): User {
  return {
    ...viewer,
    settings: { theme: settings?.theme, pageSize: settings?.pageSize ?? null },
  };
}

/** The viewer of a finished login, with the preferences it came with. */
function viewerOf(login: LoginResponse | null | undefined): User | null {
  return login?.user ? withSettings(login.user, login.preferences) : null;
}

// Three outcomes: an accepted password with the login unfinished is a 200 with no viewer.
export type SignInOutcome =
  | { stage: "signedIn" }
  | { stage: "secondFactor" }
  | { stage: "refused"; failure: GeneralError };

export async function signIn(
  credentials: LoginCredentials,
): Promise<SignInOutcome> {
  const { data, error } = await accountApi.signIn(credentials);

  if (error) return { stage: "refused", failure: error };

  // No session yet: the challenge lives in a cookie of its own.
  if (data?.twoFactorRequired) return { stage: "secondFactor" };

  // The viewer arrives wrapped, next to the preferences of the first screen.
  useAuthStore().updateUser(viewerOf(data));
  return { stage: "signedIn" };
}

/** Null once the session is in place; the viewer arrives enveloped, unlike on the first step. */
export async function completeSecondFactor(
  code: string,
): Promise<GeneralError | null> {
  const { data, error } = await accountApi.completeTwoFactorLogin({ code });

  if (error) return error;

  useAuthStore().updateUser(viewerOf(data?.resource));
  return null;
}

// Dropped only once the server confirms: a guest interface over a live session misleads.
export async function signOut(): Promise<boolean> {
  const { error } = await accountApi.signOut();
  if (error) {
    notifyFailure(error, "Не удалось выйти из аккаунта");
    return false;
  }

  useAuthStore().updateUser(null);
  return true;
}

// The others first: if they survive this one stays, so the failure is not hidden.
export async function signOutAll(): Promise<boolean> {
  const { error } = await accountApi.logoutAll();
  if (error) {
    notifyFailure(error, "Не удалось выйти со всех устройств");
    return false;
  }

  return signOut();
}

// One per store: the shell and the route guard both ask, and both get one request.
const restorations = new WeakMap<object, Promise<void>>();

// With nobody stored the server is asked anyway: the session cookie outlives the stored copy.
export function restoreSession(): Promise<void> {
  const auth = useAuthStore();
  let restoring = restorations.get(auth);
  if (!restoring) {
    restoring = auth.isAuthenticated ? fetchUser() : probeSession();
    restorations.set(auth, restoring);
  }
  return restoring;
}

/**
 * The viewer as the server has it, with the theme and the page size of the
 * account; a failed read of those keeps the ones the viewer had.
 */
async function readViewer(options?: RequestOptions): Promise<User | null> {
  const [profile, preferences] = await Promise.all([
    personalApi.getMyProfile(options),
    personalApi.getMyPreferences(options),
  ]);
  if (profile.error || !profile.data) return null;

  return withSettings(
    profile.data,
    preferences.data ?? useAuthStore().user?.settings,
  );
}

async function probeSession() {
  const viewer = await readViewer({ probesSession: true });
  if (!viewer) return;

  // A sign-in finished while the question was out has the newer answer.
  const auth = useAuthStore();
  if (!auth.isAuthenticated) auth.updateUser(viewer);
}

// A failure is no answer about the viewer: only a 401 ends the session, and the interceptor does it.
export async function fetchUser() {
  const auth = useAuthStore();
  if (!auth.isAuthenticated) return;

  const viewer = await readViewer();
  if (viewer) auth.updateUser(viewer);
}
