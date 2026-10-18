/**
 * "Нарушения", the first section of the profile's "Модерация" tab.
 *
 * The two counts read as the stat lines of the profile, and each leads where
 * its records can be read, narrowed to this user. The warnings lead to
 * "Последние предупреждения"; the bans lead to "Последние баны", written as
 * DM2's ban log, with their reasons, their moderators and their lifts. The
 * list of bans that used to open in place had none of that, left the lifted
 * bans out and stood under a count that counted them: "Баны: 11" over ten
 * rows. A count of zero leads nowhere.
 *
 * A warning issued here is a warning on the profile: the dialog is told so,
 * and the server copies the profile as it stands into the record. The dialog
 * is asked for no plate: the count in this section goes up under the
 * moderator's eyes once the parent refetches, which it is told to.
 */
import { describe, it, expect, beforeEach, vi } from "vitest";
import { mount, flushPromises, RouterLinkStub } from "@vue/test-utils";
import type {
  ViolationSummary,
  ModerationPermissions,
} from "@/shared/api/models/moderation";
import ModerationViolations from "./ModerationViolations.vue";

const getBans = vi.hoisted(() => vi.fn());

vi.mock("@/entities/moderation", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/entities/moderation")>()),
  moderationApi: { getBans },
}));

const dialogs = vi.hoisted(() => ({
  WarningDialog: { template: "<div />" },
  BanDialog: { template: "<div />" },
}));

vi.mock("@/features/moderation-actions", () => dialogs);

/**
 * The dialogs are never mounted here. What this block decides about them is in
 * the attrs it hands over, so the modal registrations are collected.
 */
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
    getBans.mockReset();
    getBans.mockResolvedValue({ data: { history: [] } });
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

  /**
   * The bans of one person are the ban list narrowed to him, the way his
   * warnings are the warning log narrowed to him: one place reads a ban, in
   * the words of DM2, lifts included.
   */
  it("leads the bans to the ban list narrowed to this user", () => {
    const link = linkOf(render({ totalBans: 11 }), "Баны");

    expect(link.text()).toBe("11");
    expect(link.props("to")).toEqual({
      name: "moderation-bans",
      query: { username: "Нарушитель" },
    });
  });

  it("opens no list in place and asks for no bans of its own", async () => {
    const wrapper = render();
    await flushPromises();

    expect(wrapper.find("[aria-expanded]").exists()).toBe(false);
    expect(wrapper.find(".mod-violations_lines button").exists()).toBe(false);
    expect(getBans).not.toHaveBeenCalled();
  });

  it("leads nowhere from a count of zero", () => {
    const wrapper = render({ totalWarnings: 0, totalBans: 0 });

    expect(lines(wrapper)).toEqual(["Предупреждения: 0", "Баны: 0"]);
    expect(wrapper.findAllComponents(RouterLinkStub)).toHaveLength(0);
    expect(wrapper.find(".mod-violations_lines button").exists()).toBe(false);
  });

  it("asks the warning dialog for no plate, and tells the profile to recount", () => {
    const wrapper = render();
    const warn = registered(dialogs.WarningDialog);

    expect(warn.attrs.hideToast).toBe(true);
    (warn.attrs.onSuccess as () => void)();
    expect(wrapper.emitted("updated")).toHaveLength(1);
  });

  /**
   * A warning issued from the profile is a warning on the profile: the server
   * keeps the avatar, the status and "О себе" as they stand, and the log
   * shows them. It used to name nothing, and the log had nothing to show.
   */
  it("tells the warning dialog the warning is on the profile", () => {
    render();
    const warn = registered(dialogs.WarningDialog);

    expect(warn.attrs.entityType).toBe("Profile");
    expect(warn.attrs.entityId).toBeUndefined();
  });

  it("tells the profile to recount after a ban", () => {
    const wrapper = render();

    (registered(dialogs.BanDialog).attrs.onSuccess as () => void)();

    expect(wrapper.emitted("updated")).toHaveLength(1);
  });

  it("offers the two actions the server allows, and none it does not", () => {
    const both = render();
    expect(both.findAll("button").map((b) => b.text())).toContain(
      "Выдать предупреждение",
    );

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
