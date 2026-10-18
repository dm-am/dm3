/**
 * @vitest-environment jsdom
 */

/**
 * The subscription to a person, read the way a visitor reads it: by the word on
 * the button and by the boxes of its panel.
 *
 * "Подписаться" is one press, as the subscription to a game is: it used to
 * open a form with a second "Подписаться" in it. "Подписан" opens the panel,
 * where a box saves the moment it is pressed and says "Сохранено" beside
 * itself, a refusal puts the box back and says why, and clearing the last box
 * ends the subscription. No press here raises a toast.
 *
 * The store is real and only the transport is faked. The defect this control
 * had lived between the two - a mutation that did not force the list to be
 * re-read, and a list that keeps itself "fresh" for a minute - so every test
 * seeds the list through a real fetch first, and ends on what is rendered, not
 * on a call count alone.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { defineComponent, h } from "vue";
import { mount, flushPromises, enableAutoUnmount } from "@vue/test-utils";
import { setActivePinia, createPinia } from "pinia";
import {
  SubscriptionSettings,
  SubscriptionTargetType,
  type Subscription,
} from "@/shared/api/models/subscriptions";

const { api } = vi.hoisted(() => ({
  api: {
    getMySubscriptions: vi.fn(),
    getById: vi.fn(),
    checkSubscription: vi.fn(),
    subscribe: vi.fn(),
    updateSettings: vi.fn(),
    unsubscribe: vi.fn(),
  },
}));

vi.mock("@/entities/subscription/api/subscriptionApi", () => ({
  default: api,
}));

import { useSubscriptionsStore } from "@/entities/subscription";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { useToast } from "@/shared/lib/composables/useToast";
import UserSubscribeButton from "./UserSubscribeButton.vue";
import UserSubscriptionPanel from "./UserSubscriptionPanel.vue";

const HERE = dirname(fileURLToPath(import.meta.url));

const USER_ID = "u-42";
const USERNAME = "Astrellan";

const {
  AuthorGameEvents: GAMES,
  AuthorBlogEvents: BLOGS,
  AuthorTopicEvents: TOPICS,
  InApp,
  Email,
} = SubscriptionSettings;
const ALL = SubscriptionSettings.UserSubscriptionDefault;

const row = (settings: number = ALL) =>
  ({
    id: "s-1",
    targetType: SubscriptionTargetType.User,
    targetId: USER_ID,
    targetUsername: USERNAME,
    settings,
    createdUtc: "2026-09-01T10:00:00Z",
  }) as Subscription;

const listed = (rows: Subscription[]) => ({
  data: { resources: rows },
  error: null,
});

const refused = (title = "") => ({
  data: null,
  error: { type: "", title, status: 404, traceId: "t-1" },
});

enableAutoUnmount(afterEach);

/**
 * The button in the place that owns its refusal line, as the row of actions
 * of a profile owns it: the line is a place's, made in that place's setup.
 */
const Host = defineComponent({
  setup() {
    const result = useActionResult();
    return () =>
      h(UserSubscribeButton, { userId: USER_ID, username: USERNAME, result });
  },
});

function render() {
  const wrapper = mount(Host, { attachTo: document.body });
  const result = wrapper
    .findComponent(UserSubscribeButton)
    .props("result") as ReturnType<typeof useActionResult>;
  return { wrapper, result };
}

type Wrapper = ReturnType<typeof render>["wrapper"];

/** The control itself: the first button, the panel's come after it. */
const trigger = (wrapper: Wrapper) => wrapper.find("button");
const panel = (wrapper: Wrapper) => wrapper.find(".subscription-panel");

/** The three boxes of the panel, in the order they are drawn. */
const boxes = (wrapper: Wrapper) =>
  wrapper.findAll<HTMLInputElement>(".subscription-panel input");

const toast = useToast();
const said = (): string[] => toast.toasts.value.map((t) => t.message);

/** Seed the list the way a profile page does, and leave it cached and fresh. */
async function seed(rows: Subscription[]) {
  api.getMySubscriptions.mockResolvedValue(listed(rows));
  await useSubscriptionsStore().fetchSubscriptions();
}

/** Press a box the way a reader does: a click, which toggles it. */
async function press(box: ReturnType<typeof boxes>[number]) {
  (box.element as HTMLInputElement).click();
  await flushPromises();
}

beforeEach(() => {
  setActivePinia(createPinia());
  vi.clearAllMocks();
  for (const item of [...toast.toasts.value]) toast.dismiss(item.id);
});

describe("one press to subscribe", () => {
  it("subscribes to all three kinds and says Подписан, with no panel", async () => {
    await seed([]);
    api.subscribe.mockResolvedValue({ data: row(), error: null });
    const { wrapper } = render();
    expect(trigger(wrapper).text()).toBe("Подписаться");

    api.getMySubscriptions.mockResolvedValue(listed([row()]));
    await trigger(wrapper).trigger("click");
    await flushPromises();

    expect(api.subscribe).toHaveBeenCalledWith({
      targetType: SubscriptionTargetType.User,
      targetId: USER_ID,
      settings: ALL,
    });
    expect(trigger(wrapper).text()).toBe("Подписан");
    expect(panel(wrapper).exists()).toBe(false);
    expect(said()).toEqual([]);
  });

  it("keeps its word and says the refusal in the line it was given", async () => {
    await seed([]);
    api.subscribe.mockResolvedValue(refused());
    const { wrapper, result } = render();

    await trigger(wrapper).trigger("click");
    await flushPromises();

    expect(trigger(wrapper).text()).toBe("Подписаться");
    expect(result.refusal.value).toBe("Не удалось подписаться на пользователя");
    expect(said()).toEqual([]);
  });

  it("says a panel is behind Подписан, and nothing is behind Подписаться", async () => {
    await seed([]);
    const { wrapper } = render();
    expect(trigger(wrapper).attributes("aria-haspopup")).toBeUndefined();

    api.getMySubscriptions.mockResolvedValue(listed([row()]));
    await useSubscriptionsStore().fetchSubscriptions(true);
    await flushPromises();
    expect(trigger(wrapper).attributes("aria-haspopup")).toBe("dialog");
    expect(trigger(wrapper).attributes("aria-expanded")).toBe("false");
  });
});

describe("the panel behind Подписан", () => {
  it("opens on the kinds the subscription holds, and closes on a second press", async () => {
    await seed([row(InApp | GAMES | TOPICS)]);
    const { wrapper } = render();

    await trigger(wrapper).trigger("click");
    expect(boxes(wrapper).map((b) => b.element.checked)).toEqual([
      true,
      false,
      true,
    ]);
    expect(trigger(wrapper).attributes("aria-expanded")).toBe("true");

    await trigger(wrapper).trigger("click");
    expect(panel(wrapper).exists()).toBe(false);
  });

  /**
   * A box is the save: no "Сохранить", no "Отмена". What the server confirmed
   * reads "Сохранено" beside the box just pressed, and the channel the
   * subscription holds goes back with the new kinds, since the server replaces
   * the whole number.
   */
  it("saves a box the moment it is pressed and says so beside it", async () => {
    await seed([row(InApp | Email | GAMES | BLOGS | TOPICS)]);
    const { wrapper } = render();
    await trigger(wrapper).trigger("click");

    const kept = InApp | Email | GAMES | BLOGS;
    api.updateSettings.mockResolvedValue({ data: row(kept), error: null });
    api.getMySubscriptions.mockResolvedValue(listed([row(kept)]));
    await press(boxes(wrapper)[2]);

    expect(api.updateSettings).toHaveBeenCalledWith("s-1", { settings: kept });
    expect(boxes(wrapper)[2].element.checked).toBe(false);
    const saved = wrapper.findAll(".category-saved");
    expect(saved).toHaveLength(1);
    expect(saved[0].element.closest("label")?.textContent).toContain("Топики");
    expect(
      wrapper.findAll(".subscription-panel button").map((b) => b.text()),
    ).toEqual(["Отписаться"]);
    expect(said()).toEqual([]);
  });

  it("puts a refused box back and says why under the list", async () => {
    await seed([row()]);
    const { wrapper } = render();
    await trigger(wrapper).trigger("click");

    api.updateSettings.mockResolvedValue(refused());
    await press(boxes(wrapper)[0]);

    expect(boxes(wrapper)[0].element.checked).toBe(true);
    expect(wrapper.get(".subscription-panel .action-result").text()).toBe(
      "Не удалось сохранить подписку",
    );
    expect(wrapper.find(".category-saved").exists()).toBe(false);
    expect(said()).toEqual([]);
  });

  /**
   * While a save is out the boxes stay drawn as they are, and a press on one
   * is held: it neither flips the box nor becomes a second request racing the
   * first.
   */
  it("holds a press made while a save is out", async () => {
    await seed([row()]);
    const { wrapper } = render();
    await trigger(wrapper).trigger("click");

    let answer!: (value: unknown) => void;
    api.updateSettings.mockReturnValue(new Promise((yes) => (answer = yes)));
    (boxes(wrapper)[0].element as HTMLInputElement).click();
    await flushPromises();
    (boxes(wrapper)[1].element as HTMLInputElement).click();
    await flushPromises();

    expect(api.updateSettings).toHaveBeenCalledTimes(1);
    expect(boxes(wrapper)[1].element.checked).toBe(true);
    expect(boxes(wrapper)[0].element.getAttribute("aria-busy")).toBe("true");

    answer({ data: row(InApp | BLOGS | TOPICS), error: null });
    await flushPromises();
  });

  /**
   * The boxes are the subscription: clearing the last one ends it, the panel
   * stays and says so, and a box ticked then subscribes again with that kind
   * alone. The old red "choose at least one" has nothing left to guard.
   */
  it("ends the subscription with the last box, and starts it again with one", async () => {
    await seed([row(InApp | BLOGS)]);
    const { wrapper } = render();
    await trigger(wrapper).trigger("click");

    api.unsubscribe.mockResolvedValue({ data: null, error: null });
    api.getMySubscriptions.mockResolvedValue(listed([]));
    await press(boxes(wrapper)[1]);

    expect(api.unsubscribe).toHaveBeenCalledWith("s-1");
    expect(trigger(wrapper).text()).toBe("Подписаться");
    expect(panel(wrapper).exists()).toBe(true);
    expect(wrapper.get(".panel-idle").text()).toBe(
      "Не подписан. Отметьте, о чем сообщать.",
    );
    expect(wrapper.text()).not.toContain("хотя бы одну");

    api.subscribe.mockResolvedValue({ data: row(InApp | TOPICS), error: null });
    api.getMySubscriptions.mockResolvedValue(listed([row(InApp | TOPICS)]));
    await press(boxes(wrapper)[2]);

    expect(api.subscribe).toHaveBeenCalledWith({
      targetType: SubscriptionTargetType.User,
      targetId: USER_ID,
      settings: InApp | TOPICS,
    });
    expect(trigger(wrapper).text()).toBe("Подписан");
    expect(boxes(wrapper).map((b) => b.element.checked)).toEqual([
      false,
      false,
      true,
    ]);
  });

  it("ends the subscription on Отписаться and closes over the new word", async () => {
    await seed([row()]);
    api.unsubscribe.mockResolvedValue({ data: null, error: null });
    const { wrapper } = render();
    await trigger(wrapper).trigger("click");

    api.getMySubscriptions.mockResolvedValue(listed([]));
    await wrapper.get(".panel-footer button").trigger("click");
    await flushPromises();

    expect(api.unsubscribe).toHaveBeenCalledWith("s-1");
    expect(panel(wrapper).exists()).toBe(false);
    expect(trigger(wrapper).text()).toBe("Подписаться");
    expect(document.activeElement).toBe(trigger(wrapper).element);
    expect(said()).toEqual([]);
  });

  it("keeps the panel open on a refused Отписаться and says why", async () => {
    await seed([row()]);
    api.unsubscribe.mockResolvedValue(refused());
    const { wrapper } = render();
    await trigger(wrapper).trigger("click");

    await wrapper.get(".panel-footer button").trigger("click");
    await flushPromises();

    expect(panel(wrapper).exists()).toBe(true);
    expect(trigger(wrapper).text()).toBe("Подписан");
    expect(wrapper.get(".subscription-panel .action-result").text()).toBe(
      "Не удалось отписаться от пользователя",
    );
  });
});

describe("leaving the panel", () => {
  it("closes on Escape and gives the focus back to the button", async () => {
    await seed([row()]);
    const { wrapper } = render();
    await trigger(wrapper).trigger("click");

    await panel(wrapper).trigger("keydown", { key: "Escape" });

    expect(panel(wrapper).exists()).toBe(false);
    expect(document.activeElement).toBe(trigger(wrapper).element);
  });

  it("closes on a click beside it", async () => {
    await seed([row()]);
    const { wrapper } = render();
    await trigger(wrapper).trigger("click");

    document.body.dispatchEvent(new MouseEvent("click", { bubbles: true }));
    await flushPromises();

    expect(panel(wrapper).exists()).toBe(false);
  });

  /**
   * A panel closed while its request is out would take the refusal with it,
   * and nothing on the page would say the box was not saved.
   */
  it("stays for the answer of a save that is out, and says the refusal in it", async () => {
    await seed([row()]);
    const { wrapper } = render();
    await trigger(wrapper).trigger("click");

    let answer!: (value: unknown) => void;
    api.updateSettings.mockReturnValue(new Promise((yes) => (answer = yes)));
    (boxes(wrapper)[0].element as HTMLInputElement).click();
    await flushPromises();

    await panel(wrapper).trigger("keydown", { key: "Escape" });
    document.body.dispatchEvent(new MouseEvent("click", { bubbles: true }));
    await trigger(wrapper).trigger("click");
    await flushPromises();
    expect(panel(wrapper).exists()).toBe(true);

    answer(refused());
    await flushPromises();
    expect(wrapper.get(".subscription-panel .action-result").text()).toBe(
      "Не удалось сохранить подписку",
    );
  });
});

/**
 * The panel hangs from the button's own box, as it did before 5 October 2026:
 * the button stands in the row of actions of a profile at every width (a row
 * of buttons, a column where it does not fit), never in a menu "…", so there
 * is nothing else for the panel to hang from and no other layer to draw it in.
 * The wave of 5 October hung it from "…" and drew it in the body.
 */
describe("where the panel hangs", () => {
  it("draws the panel in the button's own box, anchored on it", async () => {
    await seed([row()]);
    const { wrapper } = render();
    await trigger(wrapper).trigger("click");

    const box = wrapper.get(".user-subscribe").element;
    expect(box.contains(panel(wrapper).element)).toBe(true);
    expect(wrapper.findComponent(UserSubscriptionPanel).props("anchor")).toBe(
      box,
    );
  });

  it("reads nothing of a row that puts its items into a menu", () => {
    const source = readFileSync(
      resolve(HERE, "UserSubscribeButton.vue"),
      "utf8",
    );
    expect(source).not.toMatch(/FitRow|useFitAnchor|Teleport/);
  });
});
