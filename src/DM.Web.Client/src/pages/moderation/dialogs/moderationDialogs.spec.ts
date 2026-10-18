/**
 * @vitest-environment jsdom
 */

/**
 * The moderation windows with a form, as they answer a refusal.
 *
 * Each of them used to run its request by hand: a busy flag, a catch around a
 * client that never throws, and a line under the first field that nothing ever
 * filled, so a refusal fell as a plate over a window that closed as if the save
 * had gone through. Now the window runs the request itself (Dialog `action`),
 * and what is left for each window to get right is its half of the wiring: the
 * request carries the flags that keep the interceptor quiet, a refusal stands
 * in the window, the window stays open over it, and a window with typed
 * multi-line text asks before dropping it. The rest of the cycle is the
 * window's own and has its spec in shared/ui/Layout/Dialog.spec.ts.
 *
 * The two windows of the awards page have specs of their own.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { defineComponent, h, type Component } from "vue";
import {
  enableAutoUnmount,
  flushPromises,
  mount,
  type VueWrapper,
} from "@vue/test-utils";
import { achievementApi } from "@/entities/achievement";
import { gameTagApi } from "@/entities/game";
import { moderationApi } from "@/entities/moderation";
import { setScrollContainer } from "@/shared/lib/scroll";
import AchievementCategoryDialog from "./AchievementCategoryDialog.vue";
import AchievementTierDialog from "./AchievementTierDialog.vue";
import TagDialog from "./TagDialog.vue";
import TagGroupDialog from "./TagGroupDialog.vue";
import UsernameChangeRejectDialog from "./UsernameChangeRejectDialog.vue";

vi.mock("vue-final-modal", () => ({
  VueFinalModal: defineComponent({
    props: { contentClass: { type: String, default: "" } },
    setup:
      (props, { slots }) =>
      () =>
        h(
          "div",
          { class: ["vfm-content", props.contentClass] },
          slots.default?.(),
        ),
  }),
}));

const FLAGS = { ownsRefusal: true, ownsRateLimit: true };

const refused = {
  data: null,
  error: {
    type: "Failure",
    status: 403,
    title: "Недостаточно прав",
    traceId: "t",
  },
};

interface Case {
  name: string;
  component: Component;
  props: Record<string, unknown>;
  /** The request the window sends, spied on its real client. */
  request: () => ReturnType<typeof vi.spyOn>;
  /** Fills what the main button waits for, if anything. */
  fill?: (w: VueWrapper) => Promise<void>;
  /** The multi-line field the window asks about, if it has one. */
  typed?: string;
}

const CATEGORY = {
  id: "c-1",
  code: "POSTS",
  title: "Посты",
  description: "Сколько постов",
  iconName: "quill",
  sortOrder: 1,
  isActive: true,
};

const TIER = {
  id: "t-1",
  code: "POSTS_100",
  title: "Сотня",
  threshold: 100,
  tier: 1,
  category: { id: "c-1" },
};

const GROUP = { id: "g-1", title: "Жанр", description: "", sortOrder: 0 };

/**
 * A row of the queue of new names as the server sends it: the status by its
 * name, the empty fields left out.
 */
const NAME_REQUEST = {
  id: "r-1",
  userId: "u-1",
  currentUsername: "Иван",
  reason: "Опечатка в имени",
  status: "Pending",
  createdUtc: "2026-09-01T10:00:00Z",
};

const CASES: Case[] = [
  {
    name: "the achievement category",
    component: AchievementCategoryDialog,
    props: { category: CATEGORY },
    request: () => vi.spyOn(achievementApi, "updateAchievementCategory"),
    typed: "#achievement-cat-description",
  },
  {
    name: "a new achievement tier",
    component: AchievementTierDialog,
    props: { tier: null, categoryId: "c-1", defaultTier: 2 },
    request: () => vi.spyOn(achievementApi, "createAchievementType"),
    fill: async (w) => {
      await w.get("#tier-code").setValue("POSTS_500");
      await w.get("#tier-title").setValue("Пятьсот");
      await w.get("#tier-threshold").setValue("500");
    },
  },
  {
    name: "an achievement tier",
    component: AchievementTierDialog,
    props: { tier: TIER, categoryId: "c-1", defaultTier: 1 },
    request: () => vi.spyOn(achievementApi, "updateAchievementType"),
  },
  {
    name: "a new tag",
    component: TagDialog,
    props: {
      tag: null,
      groups: [GROUP],
      defaultGroupId: "g-1",
      defaultSortOrder: 0,
    },
    request: () => vi.spyOn(gameTagApi, "createTag"),
    fill: async (w) => {
      await w.get("#tag-title").setValue("Фэнтези");
    },
    typed: "#tag-description",
  },
  {
    name: "a tag group",
    component: TagGroupDialog,
    props: { group: GROUP, defaultSortOrder: 0 },
    request: () => vi.spyOn(gameTagApi, "updateTagGroup"),
    typed: "#tag-group-description",
  },
  {
    name: "the rejection of a new name",
    component: UsernameChangeRejectDialog,
    props: { request: NAME_REQUEST },
    request: () => vi.spyOn(moderationApi, "resolveUsernameChangeRequest"),
    fill: async (w) => {
      await w.get("#reject-reason").setValue("Имя занято");
    },
    typed: "#reject-reason",
  },
];

enableAutoUnmount(afterEach);

beforeEach(() => vi.restoreAllMocks());

afterEach(() => {
  document.body.innerHTML = "";
  setScrollContainer(null);
});

/** The window as useModal opens it, with the library's closing recorded. */
function render(c: Case) {
  const shut = vi.fn();
  const wrapper = mount(c.component, {
    attachTo: document.body,
    props: { ...c.props, modelValue: true, "onUpdate:modelValue": shut },
    global: {
      stubs: {
        Select: {
          props: ["modelValue", "options", "id"],
          template: `<select :id="id" :value="modelValue">
            <option v-for="o in options" :key="o.value" :value="o.value">{{ o.label }}</option>
          </select>`,
        },
      },
    },
  });
  return { wrapper, shut };
}

async function press(c: Case, w: VueWrapper) {
  await c.fill?.(w);
  await w.get("form").trigger("submit");
  await flushPromises();
}

describe.each(CASES)("the window for $name", (c) => {
  it("sends its request with the window's flags and closes on success", async () => {
    const request = c.request().mockResolvedValue({
      data: null,
      error: null,
    } as never);
    const { wrapper, shut } = render(c);

    await press(c, wrapper);

    expect(request).toHaveBeenCalledTimes(1);
    expect(request.mock.calls[0].at(-1)).toEqual(FLAGS);
    expect(wrapper.emitted("success")).toBeTruthy();
    expect(shut).toHaveBeenCalledWith(false);
  });

  it("prints a refusal in itself and stays open", async () => {
    c.request().mockResolvedValue(refused as never);
    const { wrapper, shut } = render(c);

    await press(c, wrapper);

    const line = wrapper.get(".form-refusal");
    expect(line.text()).toBe("Недостаточно прав");
    expect(line.attributes("role")).toBe("alert");
    expect(wrapper.emitted("success")).toBeFalsy();
    expect(wrapper.emitted("cancel")).toBeFalsy();
    expect(shut).not.toHaveBeenCalled();
  });
});

describe.each(CASES.filter((c) => c.typed))("the window for $name", (c) => {
  it("asks before dropping typed text", async () => {
    const { wrapper, shut } = render(c);

    await wrapper.get(c.typed!).setValue("Набранный текст");
    await wrapper.get(".vfm-content").trigger("keydown", { key: "Escape" });

    expect(wrapper.find(".dialog-backdrop").exists()).toBe(true);
    expect(wrapper.text()).toContain("Закрыть без сохранения?");
    expect(shut).not.toHaveBeenCalled();
    expect(wrapper.emitted("cancel")).toBeFalsy();
  });
});

/**
 * The status goes back the way the server writes it, by name. The rest of
 * the window is the shared cycle above.
 */
describe("the window for the rejection of a new name", () => {
  it("rejects with the status by name and the typed reason", async () => {
    const c = CASES.find(
      (each) => each.component === UsernameChangeRejectDialog,
    )!;
    const request = c.request().mockResolvedValue({
      data: null,
      error: null,
    } as never);
    const { wrapper } = render(c);

    await press(c, wrapper);

    expect(request).toHaveBeenCalledWith(
      "r-1",
      { status: "Rejected", comment: "Имя занято" },
      FLAGS,
    );
  });
});
