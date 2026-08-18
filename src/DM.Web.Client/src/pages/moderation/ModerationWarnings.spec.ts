/**
 * @vitest-environment jsdom
 */

/**
 * A warning has to stay readable after the content it was issued for is gone.
 *
 * The card used to hold a reason and a moderator name, and the offending text
 * lived only behind an identifier the author was free to rewrite or delete —
 * so the evidence for a warning could be erased by the person warned, and
 * nothing on the page would show it had happened. The server now hands the card
 * the text as it stood at issue time, the address of the object, and whether it
 * was edited since; these are the three things the card must actually draw.
 *
 * The rendering is verbatim on purpose: the snapshot is the source that was
 * written, and asserting the raw text is what catches a card that quietly
 * renders it as markup instead.
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { mount, flushPromises } from "@vue/test-utils";
import type { Warning } from "@/entities/moderation";
import ModerationWarnings from "./ModerationWarnings.vue";

const getAllWarnings = vi.hoisted(() => vi.fn());

// Partial: lib/labels.ts reads the BanType enum out of the same barrel.
vi.mock("@/entities/moderation", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/entities/moderation")>()),
  moderationApi: {
    getAllWarnings,
    removeWarning: vi.fn(),
  },
}));

// The gate has its own spec (roleGate.spec.ts); here the viewer is a moderator.
vi.mock("./lib/useRoleGate", () => ({
  useRoleGate: () => ({ hasAccess: true, deniedText: "" }),
}));

vi.mock("@/shared/lib/composables/useToast", () => ({
  useToast: () => ({ success: vi.fn(), error: vi.fn() }),
}));

vi.mock("@/entities/user", () => ({
  UserLink: { props: ["user"], template: "<span class='user-stub' />" },
}));

const SNAPSHOT = "Исходный текст, за который вынесено предупреждение";

const warning = (over: Partial<Warning> = {}): Warning =>
  ({
    id: "w-1",
    user: { username: "Нарушитель" },
    moderator: { username: "Модератор" },
    points: 2,
    reason: "Оскорбление",
    createdUtc: "2026-05-01T12:00:00",
    isActive: true,
    ...over,
  }) as Warning;

async function render(warnings: Warning[]) {
  getAllWarnings.mockResolvedValue({
    data: { resources: warnings },
    error: null,
  });
  const wrapper = mount(ModerationWarnings, {
    global: {
      stubs: {
        RouterLink: {
          props: ["to"],
          template: "<a class='router-link' :href='to'><slot /></a>",
        },
        PageTitle: { template: "<h1><slot /></h1>" },
        "page-title": { template: "<h1><slot /></h1>" },
        ConfirmDialog: { template: "<div />" },
        ErrorState: { template: "<div />" },
      },
    },
  });
  await flushPromises();
  return wrapper;
}

describe("the moderation warnings page", () => {
  beforeEach(() => {
    getAllWarnings.mockReset();
  });

  it("shows the offending text as it stood when the warning was issued", async () => {
    const wrapper = await render([
      warning({
        entitySnapshot: SNAPSHOT,
        entityUrl: "/forum/flood/12#comment-c-1",
        entityEditedAfterWarning: false,
      }),
    ]);

    expect(wrapper.find(".warning-evidence_text").text()).toBe(SNAPSHOT);
    expect(wrapper.find(".warning-evidence_edited").exists()).toBe(false);
  });

  it("says so when the object was edited after the warning", async () => {
    const wrapper = await render([
      warning({
        entitySnapshot: SNAPSHOT,
        entityUrl: "/forum/flood/12#comment-c-1",
        entityEditedAfterWarning: true,
      }),
    ]);

    expect(wrapper.find(".warning-evidence_text").text()).toBe(SNAPSHOT);
    expect(wrapper.find(".warning-evidence_edited").text()).toBe(
      "Объект изменен после выдачи предупреждения",
    );
  });

  it("keeps the evidence when the object is gone and has no address", async () => {
    const wrapper = await render([
      warning({ entitySnapshot: SNAPSHOT, entityUrl: null }),
    ]);

    expect(wrapper.find(".warning-evidence_text").text()).toBe(SNAPSHOT);
    expect(wrapper.find(".warning-object").exists()).toBe(false);
  });

  it("links to the object the server addressed", async () => {
    const wrapper = await render([
      warning({ entitySnapshot: SNAPSHOT, entityUrl: "/global-chat#msg-m-1" }),
    ]);

    const link = wrapper.find(".warning-object a");
    expect(link.attributes("href")).toBe("/global-chat#msg-m-1");
  });

  /**
   * A warning issued from the profile block names no content at all. Nothing is
   * drawn there rather than an empty quote box.
   */
  it("draws no evidence block for a warning that names no content", async () => {
    const wrapper = await render([warning()]);

    expect(wrapper.find(".warning-evidence").exists()).toBe(false);
    expect(wrapper.find(".warning-object").exists()).toBe(false);
  });
});
