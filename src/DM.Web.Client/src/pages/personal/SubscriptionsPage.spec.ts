/**
 * @vitest-environment jsdom
 */

/**
 * The subscriptions page printed a column of GUIDs and three of its four links
 * led nowhere: /games/<id> is not a route, /forum/topics/<guid> was read as
 * board "topics" and topic number NaN, and a profile is addressed by name while
 * the row held an identifier. The name of the target now comes down with the
 * row, and every link is a named route.
 */
import { describe, expect, it, vi, beforeEach } from "vitest";
import { flushPromises, mount, RouterLinkStub } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { subscriptionApi } from "@/entities/subscription";
import { SubscriptionTargetType } from "@/shared/api/models/subscriptions";
import { VALUE_UNAVAILABLE } from "@/shared/lib/constants/copy";
import SubscriptionsPage from "./SubscriptionsPage.vue";

const GUID = "11111111-1111-1111-1111-111111111111";

const row = (over: Record<string, unknown>) => ({
  id: "s-1",
  targetId: GUID,
  settings: 0,
  createdUtc: "2026-08-01T10:00:00Z",
  ...over,
});

async function render(resources: unknown[]) {
  const pinia = createPinia();
  setActivePinia(pinia);
  vi.spyOn(subscriptionApi, "getMySubscriptions").mockResolvedValue({
    data: { resources },
    error: null,
  } as never);

  const wrapper = mount(SubscriptionsPage, {
    global: {
      plugins: [pinia],
      stubs: { RouterLink: RouterLinkStub, PageTitle: true },
    },
  });
  await flushPromises();
  return wrapper;
}

const firstLink = (wrapper: ReturnType<typeof mount>) =>
  wrapper.findAllComponents(RouterLinkStub)[0];

describe("SubscriptionsPage", () => {
  beforeEach(() => vi.restoreAllMocks());

  it.each([
    [SubscriptionTargetType.Game, { name: "game", params: { id: GUID } }],
    [SubscriptionTargetType.Blog, { name: "blog", params: { id: GUID } }],
    [
      SubscriptionTargetType.Topic,
      { name: "forum-topic-redirect", params: { topicId: GUID } },
    ],
  ])("points target type %i at a named route", async (targetType, to) => {
    const wrapper = await render([
      row({ targetType, targetTitle: "Хроники Амбера" }),
    ]);

    expect(firstLink(wrapper).props("to")).toEqual(to);
  });

  it("addresses a user subscription by name, not by identifier", async () => {
    const wrapper = await render([
      row({
        targetType: SubscriptionTargetType.User,
        targetTitle: "SolohinLex",
        targetUsername: "SolohinLex",
      }),
    ]);

    expect(firstLink(wrapper).props("to")).toEqual({
      name: "profile",
      params: { username: "SolohinLex" },
    });
  });

  it("prints the name of the target, never its identifier", async () => {
    const wrapper = await render([
      row({
        targetType: SubscriptionTargetType.Game,
        targetTitle: "Хроники Амбера",
      }),
    ]);

    expect(wrapper.text()).toContain("Хроники Амбера");
    expect(wrapper.text()).not.toContain(GUID);
  });

  it("offers no link to a target that is gone, and says so once", async () => {
    const wrapper = await render([
      row({ targetType: SubscriptionTargetType.Game }),
    ]);

    expect(wrapper.findAllComponents(RouterLinkStub)).toHaveLength(0);
    expect(wrapper.text()).toContain(VALUE_UNAVAILABLE);
    // Unsubscribing still works — that is why the row is drawn at all.
    expect(wrapper.find(".unsubscribe-btn").exists()).toBe(true);
  });
});
