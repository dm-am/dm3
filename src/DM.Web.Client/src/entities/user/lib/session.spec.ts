/**
 * @vitest-environment jsdom
 */

// Signing out is a statement about the server, not about this browser.
import { describe, it, expect, vi, beforeEach } from "vitest";
import { setActivePinia, createPinia } from "pinia";
import type { GeneralError } from "@/shared/api/models/common";
import type { LoginCredentials } from "@/shared/api/models/account";

const {
  mockSignIn,
  mockCompleteTwoFactorLogin,
  mockSignOut,
  mockLogoutAll,
  mockGetMyProfile,
  mockGetMyPreferences,
} = vi.hoisted(() => ({
  mockSignIn: vi.fn(),
  mockCompleteTwoFactorLogin: vi.fn(),
  mockSignOut: vi.fn(),
  mockLogoutAll: vi.fn(),
  mockGetMyProfile: vi.fn(),
  mockGetMyPreferences: vi.fn(),
}));

vi.mock("../api/accountApi", () => ({
  default: {
    signIn: mockSignIn,
    completeTwoFactorLogin: mockCompleteTwoFactorLogin,
    signOut: mockSignOut,
    logoutAll: mockLogoutAll,
  },
}));

vi.mock("../api/personalApi", () => ({
  default: {
    getMyProfile: mockGetMyProfile,
    getMyPreferences: mockGetMyPreferences,
  },
}));

import { Theme } from "@/shared/api/models/personal";
import { useAuthStore } from "@/shared/stores";
import { useUiStore } from "@/shared/stores/ui";
import { useToast } from "@/shared/lib/composables/useToast";
import {
  signIn,
  completeSecondFactor,
  signOut,
  signOutAll,
  fetchUser,
  restoreSession,
} from "./session";

const viewer = { id: "user-1", username: "SolohinLex" };

/** The page size the account chose, not the default. */
const chosen = 50;

/** The preferences of that account, as GET /v1/users/me/preferences answers. */
const preferences = {
  theme: Theme.Dark,
  pageSize: chosen,
};

/** What the viewer carries of them: the theme for a new device and the page size. */
const carried = { theme: Theme.Dark, pageSize: chosen };

const credentials: LoginCredentials = {
  email: "solohin@example.com",
  password: "correct-horse",
};

type Refusal = { data: null; error: GeneralError };

/** What the client builds when the request got no answer at all. */
const lost = (): Refusal => ({
  data: null,
  error: { type: "Unknown", title: "", status: 0, traceId: "" },
});

const refused = (status: number, title = ""): Refusal => ({
  data: null,
  error: { type: "", title, status, traceId: "" },
});

const done = () => ({ data: null, error: null });

/** The store seeds itself from localStorage, so no cast to User is needed. */
function signedIn() {
  localStorage.setItem("user", JSON.stringify(viewer));
  const auth = useAuthStore();
  expect(auth.isAuthenticated).toBe(true);
  return auth;
}

const messages = () => useToast().toasts.value.map((t) => t.message);

beforeEach(() => {
  localStorage.clear();
  setActivePinia(createPinia());
  vi.clearAllMocks();
  mockGetMyPreferences.mockResolvedValue({ data: preferences, error: null });
  const { toasts, dismiss } = useToast();
  [...toasts.value].forEach((t) => dismiss(t.id));
});

describe("signOut", () => {
  // The viewer stays in, and the saying is left to the interceptor: a request
  // that never reached the server is announced once, by the sentence that knows
  // the other address of the site.
  it("keeps the session the server never confirmed it ended", async () => {
    const auth = signedIn();
    mockSignOut.mockResolvedValue(lost());

    await signOut();

    expect(auth.isAuthenticated).toBe(true);
    expect(localStorage.getItem("user")).not.toBeNull();
    expect(messages()).toEqual([]);
  });

  it("drops the viewer once the server confirms", async () => {
    const auth = signedIn();
    mockSignOut.mockResolvedValue(done());

    await signOut();

    expect(auth.isAuthenticated).toBe(false);
    expect(localStorage.getItem("user")).toBeNull();
    expect(messages()).toEqual([]);
  });
});

describe("signOutAll", () => {
  it("leaves this session alone while the other devices are still in", async () => {
    const auth = signedIn();
    mockLogoutAll.mockResolvedValue(lost());

    await signOutAll();

    expect(mockSignOut).not.toHaveBeenCalled();
    expect(auth.isAuthenticated).toBe(true);
    // Said once, by the interceptor: see the sign-out case above.
    expect(messages()).toEqual([]);
  });

  it("ends this session after the others are gone", async () => {
    const auth = signedIn();
    mockLogoutAll.mockResolvedValue(done());
    mockSignOut.mockResolvedValue(done());

    await signOutAll();

    expect(mockSignOut).toHaveBeenCalledTimes(1);
    expect(auth.isAuthenticated).toBe(false);
    expect(messages()).toEqual([]);
  });
});

describe("signIn", () => {
  it("hands back a refusal that names no field instead of a session", async () => {
    mockSignIn.mockResolvedValue(refused(403, "Аккаунт заблокирован"));

    const outcome = await signIn(credentials);

    expect(outcome).toEqual({
      stage: "refused",
      failure: expect.objectContaining({ status: 403 }),
    });
    expect(useAuthStore().isAuthenticated).toBe(false);
  });

  // POST /v1/account/login answers with an envelope, not a bare user.
  it("adopts the user the server returned", async () => {
    mockSignIn.mockResolvedValue({
      data: { user: viewer, preferences },
      error: null,
    });

    const outcome = await signIn(credentials);

    expect(outcome).toEqual({ stage: "signedIn" });
    expect(useAuthStore().user?.username).toBe("SolohinLex");
    expect(useAuthStore().user).not.toHaveProperty("preferences");
  });

  it("puts the theme and the page size of the account on the viewer", async () => {
    mockSignIn.mockResolvedValue({
      data: { user: viewer, preferences },
      error: null,
    });

    await signIn(credentials);

    expect(useAuthStore().user?.settings).toEqual(carried);
  });

  // The account theme is the starting one of a device that never chose: before
  // the viewer carried it, the device was handed undefined and kept its own.
  it("starts a device that never chose a theme on the theme of the account", async () => {
    mockSignIn.mockResolvedValue({
      data: { user: viewer, preferences },
      error: null,
    });

    await signIn(credentials);

    expect(useUiStore().theme).toBe(Theme.Dark);
  });

  it("leaves the theme a device chose as it is", async () => {
    localStorage.setItem("dm_theme", Theme.Light);
    setActivePinia(createPinia());
    mockSignIn.mockResolvedValue({
      data: { user: viewer, preferences },
      error: null,
    });

    await signIn(credentials);

    expect(useUiStore().theme).toBe(Theme.Light);
  });

  // A success with no viewer in it: the login is half-finished.
  it("reports the second factor instead of an empty session", async () => {
    mockSignIn.mockResolvedValue({
      data: { twoFactorRequired: true },
      error: null,
    });

    const outcome = await signIn(credentials);

    expect(outcome).toEqual({ stage: "secondFactor" });
    expect(useAuthStore().isAuthenticated).toBe(false);
  });
});

describe("completeSecondFactor", () => {
  it("hands back the refusal instead of dropping the viewer", async () => {
    mockCompleteTwoFactorLogin.mockResolvedValue(
      refused(400, "Код не подошел"),
    );

    const failure = await completeSecondFactor("123456");

    expect(failure?.title).toBe("Код не подошел");
    expect(useAuthStore().isAuthenticated).toBe(false);
  });

  // The second step answers enveloped where the first answers bare. Read the
  // way the first step is read, the envelope stands in the store as the viewer.
  it("takes the viewer out of the envelope", async () => {
    mockCompleteTwoFactorLogin.mockResolvedValue({
      data: {
        resource: {
          user: viewer,
          preferences,
        },
      },
      error: null,
    });

    expect(await completeSecondFactor("123456")).toBeNull();
    expect(useAuthStore().user?.username).toBe("SolohinLex");
    expect(useAuthStore().user).not.toHaveProperty("resource");
    expect(useAuthStore().user?.settings).toEqual(carried);
  });
});

describe("fetchUser", () => {
  it("keeps the viewer when the refresh failed", async () => {
    const auth = signedIn();
    mockGetMyProfile.mockResolvedValue(refused(500));

    await fetchUser();

    expect(auth.isAuthenticated).toBe(true);
    expect(localStorage.getItem("user")).not.toBeNull();
  });

  // The settings form saves and then refreshes the viewer: the lists read the size off it.
  it("brings the theme and the page size saved on the account", async () => {
    signedIn();
    mockGetMyProfile.mockResolvedValue({ data: viewer, error: null });

    await fetchUser();

    expect(useAuthStore().user?.settings).toEqual(carried);
    expect(JSON.parse(localStorage.getItem("user") ?? "null").settings).toEqual(
      carried,
    );
  });

  it("keeps the settings the viewer had when they did not load", async () => {
    signedIn();
    mockGetMyProfile.mockResolvedValue({ data: viewer, error: null });
    await fetchUser();
    mockGetMyPreferences.mockResolvedValue(refused(500));

    await fetchUser();

    expect(useAuthStore().user?.settings).toEqual(carried);
  });
});

// The browser may hold nobody while the cookie holds a session.
describe("restoreSession", () => {
  it("signs the page in when the server holds a session the browser forgot", async () => {
    mockGetMyProfile.mockResolvedValue({ data: viewer, error: null });

    await restoreSession();

    expect(useAuthStore().user?.username).toBe("SolohinLex");
    expect(JSON.parse(localStorage.getItem("user") ?? "null")).toMatchObject(
      viewer,
    );
    expect(useAuthStore().user?.settings).toEqual(carried);
    // Asked as a probe: its 401 is a guest, not an expired session.
    expect(mockGetMyProfile).toHaveBeenCalledWith({ probesSession: true });
    expect(mockGetMyPreferences).toHaveBeenCalledWith({ probesSession: true });
  });

  it("leaves a guest a guest, without a word, when the server says nobody", async () => {
    mockGetMyProfile.mockResolvedValue(refused(401));

    await restoreSession();

    expect(useAuthStore().isAuthenticated).toBe(false);
    expect(localStorage.getItem("user")).toBeNull();
    expect(messages()).toEqual([]);
  });

  it("asks once, however many times the page asks", async () => {
    mockGetMyProfile.mockResolvedValue(refused(401));

    await Promise.all([restoreSession(), restoreSession()]);
    await restoreSession();

    expect(mockGetMyProfile).toHaveBeenCalledTimes(1);
  });

  it("refreshes a stored viewer the way the refresh always did", async () => {
    signedIn();
    mockGetMyProfile.mockResolvedValue({
      data: { ...viewer, username: "Solohin" },
      error: null,
    });

    await restoreSession();

    expect(useAuthStore().user?.username).toBe("Solohin");
    // Not a probe: a 401 here is a session that ran out, and says so.
    expect(mockGetMyProfile).not.toHaveBeenCalledWith({ probesSession: true });
  });
});
