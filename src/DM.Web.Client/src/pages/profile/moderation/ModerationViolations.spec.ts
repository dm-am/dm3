/**
 * "Нарушения" of the "Модерация" tab: the counts as stat lines, each leading
 * to its log narrowed to the user, none from zero; a warning issued here is
 * one on the profile or one with no place, and the dialog raises no plate.
 */
import { describe, it, expect, beforeEach, vi } from "vitest";
import { mount, RouterLinkStub } from "@vue/test-utils";
import type {
  ViolationSummary,
  ModerationPermissions,
} from "@/shared/api/models/moderation";
import ModerationViolations from "./ModerationViolations.vue";

const dialogs = vi.hoisted(() => ({
  WarningDialog: { template: "<div />" },
  BanDialog: { template: "<div />" },
}));

vi.mock("@/features/moderation-actions", () => dialogs);

/** The dialogs are not mounted: their registrations and attrs are read. */
type ModalRegistration = {
  component: unknown;
  attrs: Record<string, unknown>;
};

const opened = vi.hoisted(() => [] as ModalRegistration[]);

vi.mock("vue-final-modal", () => ({
  useModal: (options: ModalRegistration) => {
    opened.push(options);
    return { open: vi.fn(), close: vi.fn() };
  },
}));

const registered = (component: unknown): ModalRegistration => {
  const found = opened.find((r) => r.component === component);
  if (!found) throw new Error("the dialog was never registered");
  return found;
};

/** Both warning dialogs, the one on the profile first. */
const warningDialogs = () =>
  opened.filter((r) => r.component === dialogs.WarningDialog);

const permissions = {
  canViewEmail: true,
  canViewIpAddresses: true,
  canViewLoginHistory: true,
  canViewLinkedProfiles: true,
  canViewModNotes: true,
  canCreateModNote: true,
  canIssueWarning: true,
  canIssueBan: true,
  canLiftBan: true,
  canSetModerationWatch: true,
} satisfies ModerationPermissions;

function render(over: Partial<ViolationSummary> = {}) {
  const violations: ViolationSummary = {
    totalWarnings: 2,
    activeWarningPoints: 1,
    totalBans: 2,
    isCurrentlyBanned: false,
    ...over,
  };
  return mount(ModerationViolations, {
    props: { violations, permissions, targetUsername: "Нарушитель" },
    global: { stubs: { RouterLink: RouterLinkStub } },
  });
}

/** The two count lines, as they read. */
const lines = (wrapper: ReturnType<typeof render>) =>
  wrapper
    .findAll(".mod-violations_lines > *")
    .map((line) => line.text().replace(/\s+/g, " "));

describe("the violations section", () => {
  beforeEach(() => {
    opened.length = 0;
  });

  it("reads the two counts as stat lines under the section's heading", () => {
    const wrapper = render();

    expect(wrapper.get("h2").text()).toBe("Нарушения");
    expect(lines(wrapper)).toEqual(["Предупреждения: 2", "Баны: 2"]);
  });

  /** A count's link, found by the number it is drawn on. */
  const linkOf = (wrapper: ReturnType<typeof render>, label: string) => {
    const line = wrapper
      .findAll(".mod-violations_lines > *")
      .find((one) => one.text().startsWith(label));
    if (!line) throw new Error(`no line ${label}`);
    return line.getComponent(RouterLinkStub);
  };

  it("leads the warnings to the log of warnings narrowed to this user", () => {
    const link = linkOf(render(), "Предупреждения");

    expect(link.text()).toBe("2");
    expect(link.props("to")).toEqual({
      name: "moderation-warnings",
      query: { username: "Нарушитель" },
    });
  });

  /** The bans lead to the ban list narrowed to the user, as the warnings to theirs. */
  it("leads the bans to the ban list narrowed to this user", () => {
    const link = linkOf(render({ totalBans: 11 }), "Баны");

    expect(link.text()).toBe("11");
    expect(link.props("to")).toEqual({
      name: "moderation-bans",
      query: { username: "Нарушитель" },
    });
  });

  it("leads nowhere from a count of zero", () => {
    const wrapper = render({ totalWarnings: 0, totalBans: 0 });

    expect(lines(wrapper)).toEqual(["Предупреждения: 0", "Баны: 0"]);
    expect(wrapper.findAllComponents(RouterLinkStub)).toHaveLength(0);
    expect(wrapper.find(".mod-violations_lines button").exists()).toBe(false);
  });

  it("asks the warning dialogs for no plate, and tells the profile to recount", () => {
    const wrapper = render();

    for (const warn of warningDialogs()) {
      expect(warn.attrs.hideToast).toBe(true);
      (warn.attrs.onSuccess as () => void)();
    }
    expect(wrapper.emitted("updated")).toHaveLength(2);
  });

  /**
   * One warning names the profile, whose avatar, status and "О себе" the server keeps. The other names
   * nothing: it is for what was done where nothing is moderated, a conversation or a game room.
   */
  it("tells one warning dialog the warning is on the profile, and the other no place", () => {
    render();
    const [onProfile, placeless] = warningDialogs();

    expect(onProfile.attrs.entityType).toBe("Profile");
    expect(onProfile.attrs.entityId).toBeUndefined();
    expect(placeless.attrs.entityType).toBeUndefined();
    expect(placeless.attrs.entityId).toBeUndefined();
  });

  it("tells the profile to recount after a ban", () => {
    const wrapper = render();

    (registered(dialogs.BanDialog).attrs.onSuccess as () => void)();

    expect(wrapper.emitted("updated")).toHaveLength(1);
  });

  it("offers the two actions the server allows, and none it does not", () => {
    const both = render();
    expect(both.findAll("button").map((b) => b.text())).toEqual([
      "Выдать предупреждение за профиль",
      "Выдать предупреждение без места",
      "Выдать бан",
    ]);

    const none = mount(ModerationViolations, {
      props: {
        violations: {
          totalWarnings: 0,
          activeWarningPoints: 0,
          totalBans: 0,
          isCurrentlyBanned: false,
        },
        permissions: {
          ...permissions,
          canIssueWarning: false,
          canIssueBan: false,
        },
        targetUsername: "Нарушитель",
      },
      global: { stubs: { RouterLink: RouterLinkStub } },
    });
    expect(none.find(".mod-violations_actions").exists()).toBe(false);
  });
});
