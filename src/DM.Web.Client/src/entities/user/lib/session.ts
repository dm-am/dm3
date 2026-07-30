import { Api } from "@/shared/api";
import type { BadRequestError } from "@/shared/api/models/common";
import type {
  LoginCredentials,
  RegisterCredentials,
} from "@/shared/api/models/account";
import { useAuthStore } from "@/shared/stores";
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

/** Sign up. Returns the field errors when the backend rejects the form. */
export async function register(credentials: RegisterCredentials) {
  const { error } = await accountApi.register(credentials);
  if (error && "errors" in error) return error as BadRequestError;
  return null;
}

/** Sign in (cookie-based) and adopt the returned user as the session. */
export async function signIn(credentials: LoginCredentials) {
  const { data, error } = await accountApi.signIn(credentials);

  if (data) {
    useAuthStore().updateUser(data);
    return null;
  }

  if (error && "errors" in error) {
    return error as BadRequestError;
  }

  return null;
}

export async function signOut() {
  await accountApi.signOut();
  useAuthStore().updateUser(null);
}

/**
 * "Выйти со всех устройств": terminate every other active session first,
 * then sign the current one out. The backend has no single "logout
 * everywhere" endpoint (DELETE account/sessions/others keeps the current
 * session), so combining the two calls logs the user out on all devices.
 */
export async function signOutAll() {
  await accountApi.logoutAll();
  await accountApi.signOut();
  useAuthStore().updateUser(null);
}

/**
 * Background refresh of the session user. The store is already populated from
 * localStorage when it is created, so this only reconciles it with the server.
 */
export async function fetchUser() {
  if (!Api.isAuthenticated()) return;

  const { data } = await personalApi.getMyProfile();
  useAuthStore().updateUser(data ?? null);
}
