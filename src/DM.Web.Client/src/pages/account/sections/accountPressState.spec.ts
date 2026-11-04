/**
 * @vitest-environment jsdom
 */

/** The four presses of the account page with a refusal line of their own, the shared line of ActionResult: a waiting button, a line gone on the next press, and a wait counted down. */
import {
  describe,
  expect,
  it,
  vi,
  beforeEach,
  type MockInstance,
} from "vitest";
import { flushPromises, mount, type VueWrapper } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { accountApi, personalApi } from "@/entities/user";
import { Theme } from "@/shared/api/models/personal";
import AccountSecuritySection from "./AccountSecuritySection.vue";
import AccountSessionsSection from "./AccountSessionsSection.vue";
import AccountSettingsSection from "./AccountSettingsSection.vue";

const fetchUser = vi.hoisted(() => vi.fn());
vi.mock("@/entities/user/lib/session", () => ({
  fetchUser,
  signIn: vi.fn(),
  completeSecondFactor: vi.fn(),
  signOut: vi.fn(),
  signOutAll: vi.fn(),
}));

interface Press {
  name: string;
  render: () => Promise<VueWrapper>;
  /** The request the press makes, spied on before it is pressed. */
  request: () => MockInstance;
  /** Whatever the form needs typed in before its button takes a press. */
  fill: (wrapper: VueWrapper) => Promise<void>;
  /** The block of the page the press and its line stand in. */
  block: (wrapper: VueWrapper) => ReturnType<VueWrapper["get"]>;
  /** Its button, in that block. */
  button?: string;
  /** Or the words of its button. */
  label?: string;
}

const security = async () =>
  mount(AccountSecuritySection, {
    props: { user: { username: "reader", email: "old@dm.am" } as never },
  });

const PRESSES: Press[] = [
  {
    name: "the new address",
    render: security,
    request: () => vi.spyOn(accountApi, "changeEmail"),
    fill: async (wrapper) => {
      await wrapper.get("#new-email").setValue("new@dm.am");
      await wrapper.get("#email-password").setValue("current-secret");
    },
    block: (wrapper) => wrapper.findAll(".security-block")[0],
    label: "Изменить почту",
  },
  {
    name: "the new password",
    render: security,
    request: () => vi.spyOn(accountApi, "changePassword"),
    fill: async (wrapper) => {
      await wrapper.get("#old-password").setValue("current-secret");
      await wrapper.get("#new-password").setValue("a-longer-secret");
      await wrapper.get("#confirm-password").setValue("a-longer-secret");
    },
    block: (wrapper) => wrapper.findAll(".security-block")[1],
    label: "Изменить пароль",
  },
  {
    name: "the end of the other sessions",
    render: async () => {
      const session = (id: string, isCurrent: boolean) => ({
        id,
        isCurrent,
        persistent: true,
        expirationUtc: "2027-05-01T10:00:00Z",
        createdUtc: "2026-05-01T10:00:00Z",
        deviceInfo: "Chrome, Windows",
        ipAddress: "10.0.0.1",
      });
      vi.spyOn(accountApi, "getSessions").mockResolvedValue({
        data: { resources: [session("here", true), session("other", false)] },
        error: null,
      } as never);
      const wrapper = mount(AccountSessionsSection);
      await flushPromises();
      return wrapper;
    },
    request: () => vi.spyOn(accountApi, "logoutAll"),
    fill: async () => {},
    block: (wrapper) => wrapper.get(".sessions-actions"),
    button: "button.logout-all",
  },
  {
    name: "the save of the settings",
    render: async () => {
      vi.spyOn(personalApi, "getMyPreferences").mockResolvedValue({
        data: {
          resource: {
            theme: Theme.Light,
            pageSize: 20,
            timeZoneId: "UTC",
          },
        },
        error: null,
      } as never);
      const wrapper = mount(AccountSettingsSection);
      await flushPromises();
      return wrapper;
    },
    request: () => vi.spyOn(personalApi, "updateMyPreferences"),
    fill: async (wrapper) => {
      await wrapper.get("#account-page-size").setValue("50");
    },
    block: (wrapper) => wrapper.get(".settings-content"),
  },
];

/** A request that answers when the test says so. */
function held(request: MockInstance) {
  let answer: (value: unknown) => void = () => {};
  request.mockImplementation(
    () => new Promise((resolve) => (answer = resolve)),
  );
  return (value: unknown) => answer(value);
}

const REFUSAL = { data: null, error: { status: 409, title: "Отказ" } };

async function pressed(press: Press, wrapper: VueWrapper) {
  await press.fill(wrapper);
  await buttonOf(press, wrapper).trigger("click");
}

const buttonOf = (press: Press, wrapper: VueWrapper) => {
  const block = press.block(wrapper);
  if (press.label)
    return block.findAll("button").find((b) => b.text() === press.label)!;
  return block.get(press.button ?? "button.save-button");
};

const lineOf = (press: Press, wrapper: VueWrapper) => {
  const line = press.block(wrapper).find(".action-result");
  return line.exists() ? line.text() : null;
};

describe("a press of the account page that keeps its own line", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.restoreAllMocks();
    fetchUser.mockResolvedValue(undefined);
  });

  it.each(PRESSES.map((press) => [press.name, press] as const))(
    "%s: holds its button while the request is out and gives it back with the refusal",
    async (_name, press) => {
      const wrapper = await press.render();
      const answer = held(press.request());

      await pressed(press, wrapper);
      const button = buttonOf(press, wrapper);
      expect(button.attributes("disabled")).toBeDefined();
      expect(button.attributes("aria-busy")).toBe("true");

      answer(REFUSAL);
      await flushPromises();

      expect(button.attributes("disabled")).toBeUndefined();
      expect(button.attributes("aria-busy")).toBeUndefined();
      expect(lineOf(press, wrapper)).toBe("Отказ");
    },
  );

  it.each(PRESSES.map((press) => [press.name, press] as const))(
    "%s: takes the last refusal off the line the moment it is pressed again",
    async (_name, press) => {
      const wrapper = await press.render();
      const request = press.request().mockResolvedValue(REFUSAL as never);
      await pressed(press, wrapper);
      await flushPromises();
      expect(lineOf(press, wrapper)).toBe("Отказ");

      held(request);
      await buttonOf(press, wrapper).trigger("click");

      expect(lineOf(press, wrapper)).toBeNull();
    },
  );

  // The countdown of useActionResult: the line falls each second and the button waits for zero.
  it.each(PRESSES.map((press) => [press.name, press] as const))(
    "%s: counts the wait the server asked for down in its line and keeps the button shut until it is over",
    async (_name, press) => {
      vi.useFakeTimers({ toFake: ["setInterval", "clearInterval"] });
      try {
        const wrapper = await press.render();
        press.request().mockResolvedValue({
          data: null,
          error: { status: 429, title: "", retryAfterSeconds: 2 },
        } as never);

        await pressed(press, wrapper);
        await flushPromises();

        expect(lineOf(press, wrapper)).toBe(
          "Слишком много попыток, попробуйте через 2 секунды",
        );
        expect(buttonOf(press, wrapper).attributes("disabled")).toBeDefined();

        await vi.advanceTimersByTimeAsync(1000);
        expect(lineOf(press, wrapper)).toBe(
          "Слишком много попыток, попробуйте через 1 секунду",
        );

        await vi.advanceTimersByTimeAsync(1000);
        expect(lineOf(press, wrapper)).toBeNull();
        expect(buttonOf(press, wrapper).attributes("disabled")).toBeUndefined();
      } finally {
        vi.useRealTimers();
      }
    },
  );
});
