/**
 * @vitest-environment jsdom
 */

/**
 * The subscriptions list after a mutation of it.
 *
 * Every screen that offers to subscribe or unsubscribe draws itself from this
 * one list: the button on a profile reads its caption off it, and the rows of
 * /subscriptions are it. The list is cached for a minute, which is right for
 * reading and wrong for the caller that has just changed it on the server —
 * an unforced refetch returns at the freshness check, and the deleted row goes
 * on being drawn until the minute is out or the page is reloaded.
 *
 * So these tests seed the list through a real fetch first, which is what puts
 * it inside its own cache window, and then look at what the list holds after
 * the mutation. Seeding matters: with nothing cached even the broken version
 * refetches, and nothing here would fail.
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
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

vi.mock("../api/subscriptionApi", () => ({ default: api }));

import { useSubscriptionsStore } from "./store";

const GAME_ID = "g-1";
const USER_ID = "u-42";

const gameRow: Subscription = {
  id: "s-game",
  targetType: SubscriptionTargetType.Game,
  targetId: GAME_ID,
  targetTitle: "Вечный полдень",
  settings: SubscriptionSettings.GameReaderDefault,
  createdUtc: "2026-09-01T10:00:00Z",
};

const userRow = (
  settings = SubscriptionSettings.UserSubscriptionDefault,
): Subscription => ({
  id: "s-user",
  targetType: SubscriptionTargetType.User,
  targetId: USER_ID,
  targetUsername: "Astrellan",
  settings,
  createdUtc: "2026-09-02T10:00:00Z",
});

const listed = (rows: Subscription[]) => ({
  data: { resources: rows },
  error: null,
});

const refused = {
  data: null,
  error: { type: "", title: "Подписка не найдена", status: 404, traceId: "t" },
};

/** Read the list the way a page does, leaving it cached and fresh. */
async function seed(rows: Subscription[]) {
  api.getMySubscriptions.mockResolvedValue(listed(rows));
  const store = useSubscriptionsStore();
  await store.fetchSubscriptions();
  return store;
}

beforeEach(() => {
  setActivePinia(createPinia());
  vi.clearAllMocks();
});

describe("the list after a mutation of it", () => {
  /**
   * The row disappearing is what a reader of /subscriptions sees: that page
   * renders one row per subscription and offers no other feedback for the
   * unsubscribe it just performed.
   */
  it("drops the row that was unsubscribed from", async () => {
    const store = await seed([gameRow, userRow()]);
    api.unsubscribe.mockResolvedValue({ data: null, error: null });
    api.getMySubscriptions.mockResolvedValue(listed([gameRow]));

    expect(await store.unsubscribe("s-user")).toBeNull();

    expect(store.subscriptions).toEqual([gameRow]);
    expect(store.totalCount).toBe(1);
    expect(store.isSubscribed(SubscriptionTargetType.User, USER_ID)).toBe(
      false,
    );
  });

  it("drops the row when the target, not the id, was named", async () => {
    const store = await seed([gameRow]);
    api.unsubscribe.mockResolvedValue({ data: null, error: null });
    api.getMySubscriptions.mockResolvedValue(listed([]));

    expect(
      await store.unsubscribeByTarget(SubscriptionTargetType.Game, GAME_ID),
    ).toBeNull();

    expect(api.unsubscribe).toHaveBeenCalledWith("s-game");
    expect(store.subscriptions).toEqual([]);
  });

  it("shows the row that was just subscribed to", async () => {
    const store = await seed([]);
    api.subscribe.mockResolvedValue({ data: userRow(), error: null });
    api.getMySubscriptions.mockResolvedValue(listed([userRow()]));

    expect(
      await store.subscribe(SubscriptionTargetType.User, USER_ID),
    ).toBeNull();

    expect(store.isSubscribed(SubscriptionTargetType.User, USER_ID)).toBe(true);
    expect(store.userCount).toBe(1);
  });

  it("shows the settings that were just saved", async () => {
    const quieter =
      SubscriptionSettings.UserSubscriptionDefault &
      ~SubscriptionSettings.AuthorGameEvents;
    const store = await seed([userRow()]);
    api.updateSettings.mockResolvedValue({
      data: userRow(quieter),
      error: null,
    });
    api.getMySubscriptions.mockResolvedValue(listed([userRow(quieter)]));

    expect(await store.updateSettings("s-user", quieter)).toBeNull();

    expect(
      store.getSubscription(SubscriptionTargetType.User, USER_ID)?.settings,
    ).toBe(quieter);
  });

  /**
   * The other half of the rule: nothing changed on the server, so nothing is
   * re-read, and the caller gets the problem document to say so.
   */
  it("leaves the list alone when the server refused", async () => {
    const store = await seed([userRow()]);
    api.unsubscribe.mockResolvedValue(refused);

    expect(await store.unsubscribe("s-user")).toEqual(refused.error);

    expect(api.getMySubscriptions).toHaveBeenCalledTimes(1);
    expect(store.subscriptions).toEqual([userRow()]);
  });
});
