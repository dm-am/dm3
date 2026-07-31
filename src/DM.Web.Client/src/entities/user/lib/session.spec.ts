/**
 * @vitest-environment jsdom
 */

/**
 * Signing out is a statement about the server, not about this browser.
 *
 * All five operations used to read the answer as if it could only be a success.
 * The two sign-outs cleared the viewer whatever came back, so a refused or lost
 * request produced a guest interface over a session the cookie keeps alive for
 * a year; `register` and `signIn` returned null for any refusal that named no
 * field, which is the same value they return for a completed one; and
 * `fetchUser` wrote the empty answer through, signing people out of the
 * interface on a 500.
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { setActivePinia, createPinia } from "pinia";
import type { GeneralError } from "@/shared/api/models/common";
import type {
  LoginCredentials,
  RegisterCredentials,
} from "@/shared/api/models/account";

const {
  mockRegister,
  mockSignIn,
  mockSignOut,
  mockLogoutAll,
  mockGetMyProfile,
} = vi.hoisted(() => ({
  mockRegister: vi.fn(),
  mockSignIn: vi.fn(),
  mockSignOut: vi.fn(),
  mockLogoutAll: vi.fn(),
  mockGetMyProfile: vi.fn(),
}));

vi.mock("../api/accountApi", () => ({
  default: {
    register: mockRegister,
    signIn: mockSignIn,
    signOut: mockSignOut,
    logoutAll: mockLogoutAll,
  },
}));

vi.mock("../api/personalApi", () => ({
  default: { getMyProfile: mockGetMyProfile },
}));

import { useAuthStore } from "@/shared/stores";
import { useToast } from "@/shared/lib/composables/useToast";
import { register, signIn, signOut, signOutAll, fetchUser } from "./session";

const viewer = { id: "user-1", username: "SolohinLex" };

const credentials: LoginCredentials = {
  email: "solohin@example.com",
  password: "correct-horse",
};

const signUp: RegisterCredentials = {
  email: "solohin@example.com",
  password: "correct-horse",
  acceptedRules: true,
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
  const { toasts, dismiss } = useToast();
  [...toasts.value].forEach((t) => dismiss(t.id));
});

describe("signOut", () => {
  it("keeps the session the server never confirmed it ended", async () => {
    const auth = signedIn();
    mockSignOut.mockResolvedValue(lost());

    await signOut();

    expect(auth.isAuthenticated).toBe(true);
    expect(localStorage.getItem("user")).not.toBeNull();
    expect(messages()).toEqual(["Не удалось выйти"]);
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
    expect(messages()).toEqual(["Не удалось выйти со всех устройств"]);
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

    const failure = await signIn(credentials);

    expect(failure?.status).toBe(403);
    expect(useAuthStore().isAuthenticated).toBe(false);
  });

  it("adopts the user the server returned", async () => {
    mockSignIn.mockResolvedValue({ data: viewer, error: null });

    const failure = await signIn(credentials);

    expect(failure).toBeNull();
    expect(useAuthStore().user?.username).toBe("SolohinLex");
  });
});

describe("register", () => {
  it("hands back a rate limit instead of a created account", async () => {
    mockRegister.mockResolvedValue(refused(429));

    const failure = await register(signUp);

    expect(failure?.status).toBe(429);
  });

  it("reports nothing when the account was created", async () => {
    mockRegister.mockResolvedValue(done());

    expect(await register(signUp)).toBeNull();
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
});
