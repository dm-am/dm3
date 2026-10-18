/**
 * @vitest-environment jsdom
 */

/**
 * The account page, and the one thing on it that happens in three blocks at
 * once.
 *
 * Changing the password ends every other session and writes the journal entry
 * inside the same server call (PasswordChangeService). The block that changes
 * it is the first of the three; the device list and the journal are further
 * down and read themselves once, in onMounted. So the screen after the press
 * went on offering to sign out of devices that were already signed out, and the
 * journal went on showing an account nothing had happened to.
 *
 * Asking for a new address writes a journal entry too, and only that: the
 * address moves when the link in the letter is followed, and no session is
 * touched.
 *
 * These tests read the screen the reader is left with, not the calls behind it:
 * the device that should be gone, the offer that should be gone with it, and
 * the line the journal should have gained.
 */
import { describe, expect, it, vi, beforeEach } from "vitest";
import { flushPromises, mount, RouterLinkStub } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { accountApi } from "@/entities/user";
import { useAuthStore } from "@/shared/stores";
import type { SecurityEvent, SessionInfo } from "@/shared/api/models/account";
import AccountPage from "./AccountPage.vue";

/** The account refresh behind a sent letter; this page only awaits it. */
const fetchUser = vi.hoisted(() => vi.fn());
vi.mock("@/entities/user/lib/session", () => ({
  fetchUser,
  register: vi.fn(),
  signIn: vi.fn(),
  completeSecondFactor: vi.fn(),
  signOut: vi.fn(),
  signOutAll: vi.fn(),
}));

/**
 * Every block of the page that has nothing to do with this: each one loads
 * itself on mount, and none of them is what is being read here.
 */
const QUIET_BLOCKS = [
  "AccountInvitationsSection",
  "AccountTwoFactorSection",
  "AccountSettingsSection",
  "AccountUsernameChangeSection",
  "AccountBlacklistSection",
  "AccountNotificationsSection",
];

const session = (over: Partial<SessionInfo> = {}): SessionInfo => ({
  id: "session-1",
  isCurrent: false,
  persistent: false,
  expirationUtc: "2026-09-30T10:00:00Z",
  createdUtc: "2026-09-20T10:00:00Z",
  deviceInfo: "Ubuntu, Firefox",
  ipAddress: "10.0.0.2",
  ...over,
});

const event = (over: Partial<SecurityEvent> = {}): SecurityEvent => ({
  id: "event-1",
  eventType: "PasswordChange",
  timestampUtc: "2026-09-20T10:00:00Z",
  ...over,
});

const THIS_DEVICE = session({
  id: "session-0",
  isCurrent: true,
  deviceInfo: "Windows, Chrome",
});
const OTHER_DEVICE = session();

/**
 * Mounts the page with the three blocks that talk to each other, answering the
 * device list and the journal with what the server holds before the press and
 * then with what it holds after it.
 */
async function render(
  sessionsAfter: SessionInfo[] = [THIS_DEVICE],
  eventsAfter: SecurityEvent[] = [],
) {
  setActivePinia(createPinia());
  useAuthStore().user = { username: "reader", email: "reader@dm.am" } as never;
  fetchUser.mockResolvedValue(undefined);

  vi.spyOn(accountApi, "getSessions")
    .mockResolvedValueOnce({
      data: { resources: [THIS_DEVICE, OTHER_DEVICE] },
      error: null,
    } as never)
    .mockResolvedValue({
      data: { resources: sessionsAfter },
      error: null,
    } as never);
  vi.spyOn(accountApi, "getSecurityHistory")
    .mockResolvedValueOnce({ data: { resources: [] }, error: null } as never)
    .mockResolvedValue({
      data: { resources: eventsAfter },
      error: null,
    } as never);

  const wrapper = mount(AccountPage, {
    global: {
      stubs: {
        ...Object.fromEntries(
          QUIET_BLOCKS.map((name) => [name, { template: "<div />" }]),
        ),
        RouterLink: RouterLinkStub,
        PageTitle: { template: "<h1><slot /></h1>" },
        "page-title": { template: "<h1><slot /></h1>" },
      },
    },
  });
  await flushPromises();
  return wrapper;
}

/** What the device list and the journal are showing right now. */
const devices = (wrapper: ReturnType<typeof mount>) =>
  wrapper.get(".sessions-content").text();
const journal = (wrapper: ReturnType<typeof mount>) =>
  wrapper.get(".security-history-content").text();

/** Fills the password card of the security block and presses it. */
async function changePassword(wrapper: ReturnType<typeof mount>) {
  await wrapper.get("#old-password").setValue("current-secret");
  await wrapper.get("#new-password").setValue("a-longer-secret");
  await wrapper.get("#confirm-password").setValue("a-longer-secret");
  await wrapper
    .findAll(".security-block")[1]
    .get("button.save-button")
    .trigger("click");
  await flushPromises();
}

/** Fills the address card of the security block and presses it. */
async function askForNewAddress(wrapper: ReturnType<typeof mount>) {
  await wrapper.get("#new-email").setValue("new@dm.am");
  await wrapper.get("#email-password").setValue("current-secret");
  await wrapper
    .findAll(".security-block")[0]
    .get("button.save-button")
    .trigger("click");
  await flushPromises();
}

describe("the account page after a change of credentials", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  it("drops the sessions the password change ended, and the offer to end them", async () => {
    vi.spyOn(accountApi, "changePassword").mockResolvedValue({
      data: null,
      error: null,
    } as never);
    const wrapper = await render([THIS_DEVICE], [event()]);

    expect(devices(wrapper)).toContain("Ubuntu, Firefox");
    expect(devices(wrapper)).toContain("Выйти со всех других устройств");

    await changePassword(wrapper);

    expect(devices(wrapper)).not.toContain("Ubuntu, Firefox");
    expect(devices(wrapper)).not.toContain("Выйти со всех других устройств");
  });

  it("shows the journal entry the password change wrote", async () => {
    vi.spyOn(accountApi, "changePassword").mockResolvedValue({
      data: null,
      error: null,
    } as never);
    const wrapper = await render([THIS_DEVICE], [event()]);

    expect(journal(wrapper)).toContain("Нет событий безопасности");

    await changePassword(wrapper);

    expect(journal(wrapper)).toContain("Изменение пароля");
  });

  it("shows the journal entry the request for a new address wrote", async () => {
    vi.spyOn(accountApi, "changeEmail").mockResolvedValue({
      data: null,
      error: null,
    } as never);
    const wrapper = await render(
      [THIS_DEVICE, OTHER_DEVICE],
      [event({ eventType: "EmailChange" })],
    );

    await askForNewAddress(wrapper);

    expect(journal(wrapper)).toContain("Запрос смены почты");
    // No session is touched by the request, so the list is left alone: the
    // other device is still there to be signed out of.
    expect(devices(wrapper)).toContain("Ubuntu, Firefox");
  });
});
