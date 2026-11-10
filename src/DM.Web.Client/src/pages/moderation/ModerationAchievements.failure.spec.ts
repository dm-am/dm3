/**
 * @vitest-environment jsdom
 */

// A refused read of the catalogue says so and offers a retry, and keeps
// nothing, so the next visit reads again.
import { describe, it, expect, vi, afterEach } from "vitest";
import { enableAutoUnmount, flushPromises } from "@vue/test-utils";
import ModerationAchievements from "./ModerationAchievements.vue";
import { mountPage } from "@/shared/lib/testing/mountPage";

const api = vi.hoisted(() => ({
  getModerationAchievementCategories: vi.fn(),
  getModerationAchievementTypes: vi.fn(),
}));

vi.mock("@/entities/achievement/api/achievementApi", () => ({ default: api }));

enableAutoUnmount(afterEach);

const CATEGORY = {
  id: "c-1",
  title: "Игровые посты",
  description: "Игровые посты во всех играх",
  code: "game_posts_authored",
  metric: "GamePostsAuthored",
  iconName: "quill",
  sortOrder: 1,
  isActive: true,
};

function answer(ok: boolean) {
  api.getModerationAchievementCategories.mockResolvedValue(
    ok
      ? { data: { resources: [CATEGORY] }, error: null }
      : { data: null, error: { status: 500, title: "Ошибка" } },
  );
  api.getModerationAchievementTypes.mockResolvedValue({
    data: { resources: [] },
    error: null,
  });
}

async function render() {
  const { wrapper } = await mountPage(
    ModerationAchievements,
    "/moderation/achievements",
    { routes: [["moderation-achievements", "/moderation/achievements"]] },
  );
  await flushPromises();
  return wrapper;
}

describe("a refused read of the achievement catalogue", () => {
  // One case: the catalogue lives in the module between visits.
  it("says so, is read again on the next visit, and retries", async () => {
    answer(false);
    const first = await render();
    expect(first.get(".error-state .error-message").text()).toBe(
      "Не удалось загрузить достижения",
    );
    expect(first.find(".cat-list").exists()).toBe(false);
    first.unmount();

    const second = await render();
    expect(api.getModerationAchievementCategories).toHaveBeenCalledTimes(2);
    const error = second.get(".error-state");

    answer(true);
    await error.get(".error-retry").trigger("click");
    await flushPromises();

    expect(second.find(".error-state").exists()).toBe(false);
    expect(second.get(".cat-row .cat-title").text()).toBe("Игровые посты");
  });
});
