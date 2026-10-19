/**
 * @vitest-environment jsdom
 */

// The two premoderation queues share PremoderatedQueue: the merge of both
// buckets, oldest first, each queue's owner column and links.
import { describe, it, expect, vi, beforeEach } from "vitest";
import { mount, flushPromises } from "@vue/test-utils";
import { createMemoryHistory, createRouter } from "vue-router";
import type { Component } from "vue";
import PageTitle from "@/shared/ui/Layout/PageTitle.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import FormField from "@/shared/ui/Form/FormField.vue";
import ModerationPremoderatedBlogs from "./ModerationPremoderatedBlogs.vue";
import ModerationPremoderatedGames from "./ModerationPremoderatedGames.vue";

const getPremoderatedBlogs = vi.hoisted(() => vi.fn());
const getPremoderatedGames = vi.hoisted(() => vi.fn());

// Partial: lib/labels.ts reads its enums from this same barrel.
vi.mock("@/entities/moderation", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/entities/moderation")>()),
  moderationApi: { getPremoderatedBlogs, getPremoderatedGames },
}));

const router = createRouter({
  history: createMemoryHistory(),
  routes: [
    { path: "/blogs/:id", name: "blog", component: { template: "<div />" } },
    { path: "/games/:id", name: "game", component: { template: "<div />" } },
    {
      path: "/u/:username",
      name: "profile",
      component: { template: "<div />" },
    },
  ],
});

/** One row, spelled the same for both queues so only the words differ. */
const ROW = {
  id: "queue-row",
  publicId: "",
  title: "Название",
  author: { username: "Владеющий" },
  master: { username: "Владеющий" },
  createdUtc: "2026-05-01",
};

async function render(page: Component) {
  const wrapper = mount(page, {
    global: {
      plugins: [router],
      components: {
        PageTitle,
        "page-title": PageTitle,
        SecondaryText,
        "secondary-text": SecondaryText,
        FormField,
      },
    },
  });
  await flushPromises();
  return wrapper;
}

beforeEach(() => {
  getPremoderatedBlogs.mockReset();
  getPremoderatedGames.mockReset();
});

/** Both buckets answer, so the merge and the sort run: the second bucket's row is older and on top. */
function answerWith(row: object | null) {
  const reply = async (status: string) => ({
    data: {
      resources: row
        ? [
            {
              ...row,
              id: `${(row as { id: string }).id}-${status}`,
              createdUtc:
                status === "AwaitingApproval" ? "2026-05-01" : "2026-04-01",
            },
          ]
        : [],
      paging: null,
    },
    error: null,
  });
  getPremoderatedBlogs.mockImplementation(reply);
  getPremoderatedGames.mockImplementation(reply);
}

describe("premoderation queues", () => {
  it("says that the queue is empty", async () => {
    answerWith(null);

    const blogs = await render(ModerationPremoderatedBlogs);
    const games = await render(ModerationPremoderatedGames);

    expect(blogs.find(".secondary-text").text()).toBe(
      "Премодерируемых блогов пока нет",
    );
    expect(games.find(".secondary-text").text()).toBe(
      "Премодерируемых игр пока нет",
    );
  });

  it("says which queue failed to load", async () => {
    const refused = { data: null, error: { status: 500 } };
    getPremoderatedBlogs.mockResolvedValue(refused);
    getPremoderatedGames.mockResolvedValue(refused);

    const blogs = await render(ModerationPremoderatedBlogs);
    const games = await render(ModerationPremoderatedGames);

    expect(blogs.find(".error-state").text()).toContain(
      "Не удалось загрузить премодерируемые блоги",
    );
    expect(games.find(".error-state").text()).toContain(
      "Не удалось загрузить премодерируемые игры",
    );
  });

  it("gives each queue its own columns and links", async () => {
    answerWith({ ...ROW, publicId: "the-public-id" });

    const blogs = await render(ModerationPremoderatedBlogs);
    const games = await render(ModerationPremoderatedGames);

    // The owner column keeps its own key: DataTable spells it into the cell class.
    expect(blogs.find("td.col-author").text()).toBe("Владеющий");
    expect(games.find("td.col-master").text()).toBe("Владеющий");

    expect(blogs.find("td.col-title a").attributes("href")).toBe(
      "/blogs/queue-row-AwaitingEdits",
    );
    // A game is addressed by its public id when it has one; a blog has none.
    expect(games.find("td.col-title a").attributes("href")).toBe(
      "/games/the-public-id",
    );
  });
});
