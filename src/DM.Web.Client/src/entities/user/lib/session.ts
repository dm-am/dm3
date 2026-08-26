import type {
  LoginCredentials,
  RegisterCredentials,
} from "@/shared/api/models/account";
import type { GeneralError } from "@/shared/api/models/common";
import { useAuthStore } from "@/shared/stores";
import { notifyFailure } from "@/shared/lib/errors";
import accountApi from "../api/accountApi";
import personalApi from "../api/personalApi";

/**
 * The operations that establish, refresh and end a session.
 *
 * They used to be actions on the session store, which put account endpoints
 * inside `shared` — the one layer that must stay domain-free. The store keeps
 * the state (who the viewer is, persistence, cross-tab sync); the calls that
 * change it live here, with the rest of the account surface.
 */

/**
 * Sign up. Returns the problem document to answer with, or null once the
 * pending registration exists and the letter is on its way.
 *
 * `null` is the caller's word for success, and it used to be the answer to
 * every failure that carried no field errors as well: an attempt the rate
 * limit refused, a mail server that would not take the letter. The form went
 * on to the "Проверьте почту" screen, and the reader waited for a letter
 * nobody had sent.
 */
export async function register(credentials: RegisterCredentials) {
  const { error } = await accountApi.register(credentials);
  return error;
}

/**
 * Where a sign-in attempt stopped.
 *
 * Three outcomes and not two, because the server has three: a session, a
 * refusal, and a password that was accepted while the login is not finished.
 * The third one is a 200 with no viewer in it — the shape a boolean "did it
 * fail" reads as success — so it has to be a stage of its own here or the
 * dialog closes over a viewer who never got in.
 */
export type SignInOutcome =
  | { stage: "signedIn" }
  | { stage: "secondFactor" }
  | { stage: "refused"; failure: GeneralError };

/**
 * Sign in (cookie-based) and adopt the returned user as the session.
 *
 * A 403 is the one that mattered: the server refuses a banned, removed or
 * locked-out account by name, and that name is the answer to the form.
 * Reported as success, it closed the dialog over a header that went on
 * offering "Вход | Регистрация".
 */
export async function signIn(
  credentials: LoginCredentials,
): Promise<SignInOutcome> {
  const { data, error } = await accountApi.signIn(credentials);

  if (error) return { stage: "refused", failure: error };

  // The password was accepted and the second factor is still owed: no session
  // exists, no cookie was set, and the viewer is deliberately absent from the
  // body. The challenge lives in a cookie of its own, so there is nothing to
  // hold on to here - only a stage to report.
  if (data?.twoFactorRequired) return { stage: "secondFactor" };

  // The viewer arrives wrapped, next to the preferences of the first screen.
  // Stored whole, the envelope stood in the store where the viewer belongs:
  // every field read off it was undefined, the header went on offering
  // "Вход | Регистрация" over a live session, and it stayed that way until the
  // next boot reconciled the store against the server.
  // No body behind a success is not a session, and the two lines above already
  // ruled out a refusal and a half-finished login: nothing to sign in as, so
  // the store stays empty.
  useAuthStore().updateUser(data?.user ?? null);
  return { stage: "signedIn" };
}

/**
 * Finish a login with the code from the device or with a recovery code.
 * Returns the problem document to answer with, or null once the session is in
 * place.
 *
 * The viewer arrives inside an envelope here, unlike on the first step, which
 * answers with a bare body. Read the way the first step is read, the envelope
 * itself would stand in the store where the viewer belongs.
 */
export async function completeSecondFactor(
  code: string,
): Promise<GeneralError | null> {
  const { data, error } = await accountApi.completeTwoFactorLogin({ code });

  if (error) return error;

  useAuthStore().updateUser(data?.resource?.user ?? null);
  return null;
}

/**
 * End the current session.
 *
 * The viewer is dropped only once the server confirms its own session is gone.
 * The cookie is HttpOnly and lives a year with "Запомнить меня", so clearing
 * the store on a failed request paints a guest interface over a live session:
 * on a shared machine that is the outcome this action exists to prevent.
 */
export async function signOut(): Promise<boolean> {
  const { error } = await accountApi.signOut();
  if (error) {
    notifyFailure(error, "Не удалось выйти");
    return false;
  }

  useAuthStore().updateUser(null);
  return true;
}

/**
 * "Выйти со всех устройств": terminate every other active session first,
 * then sign the current one out. The backend has no single "logout
 * everywhere" endpoint (DELETE account/sessions/others keeps the current
 * session), so combining the two calls logs the user out on all devices.
 *
 * The order decides what a failure means as well: if the other sessions
 * survive, this one is left alone too. Signing out here would hide the half
 * that failed behind a guest interface, and someone who asked for every device
 * would have no way to learn that the other devices are still signed in.
 */
export async function signOutAll(): Promise<boolean> {
  const { error } = await accountApi.logoutAll();
  if (error) {
    notifyFailure(error, "Не удалось выйти со всех устройств");
    return false;
  }

  return signOut();
}

/**
 * Background refresh of the session user. The store is already populated from
 * localStorage when it is created, so this only reconciles it with the server.
 *
 * A failed request is not an answer about who the viewer is. Only a 401 says
 * the session is over, and the client interceptor already ends it; anything
 * else is a bad minute on the network. Writing the empty result through signed
 * people out of the interface on a 500, with the server session still alive.
 */
export async function fetchUser() {
  const auth = useAuthStore();
  if (!auth.isAuthenticated) return;

  const { data, error } = await personalApi.getMyProfile();
  if (error || !data) return;

  auth.updateUser(data);
}
