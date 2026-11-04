/**
 * @vitest-environment jsdom
 */

// The board listing: the refusal strip, the last activity, the sorted column.
// The pins are TopicsList.pins and pinnedOrder specs', the form TopicCreateForm's.
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { enableAutoUnmount, type VueWrapper } from "@vue/test-utils";
import { VALUE_UNAVAILABLE } from "@/shared/lib/constants/copy";
import { DATE_TIME_FORMAT, formatSiteTime } from "@/shared/lib/utils/datetime";
import { mockGetTopics } from "./TopicsList.doubles";
import {
  refusal,
  topic,
  PLAIN,
  answerBoard,
  freshPage,
  openBoard,
  rowOf,
  signIn,
} from "./TopicsList.fixtures";

// The network only: the router, the store, the table and the links are the real ones.
vi.mock("@/entities/forum/api/forumApi", async () =>
  (await import("./TopicsList.doubles")).forumApi(),
);

enableAutoUnmount(afterEach);

// The strip owns the refusal of the reads behind it and says the whole
// sentence; an expired session and a dead connection stay with the interceptor.
describe("TopicsList refusal strip", () => {
  beforeEach(() => {
    freshPage();
    signIn("reader");
  });

  it("stands the server's own words where the rows would be", async () => {
    mockGetTopics.mockResolvedValue({
      data: null,
      error: { ...refusal(403), title: "Раздел закрыт для чтения" },
    });

    const wrapper = await openBoard();

    expect(wrapper.get(".error-state .error-message").text()).toBe(
      "Раздел закрыт для чтения",
    );
    expect(wrapper.find("tr.table-row").exists()).toBe(false);
  });

  it("counts out the wait a rate limit asked for", async () => {
    mockGetTopics.mockResolvedValue({
      data: null,
      error: {
        ...refusal(429),
        title: "Слишком много запросов",
        retryAfterSeconds: 30,
      },
    });

    const wrapper = await openBoard();

    // The seconds are the part a reader acts on.
    expect(wrapper.get(".error-state .error-message").text()).toBe(
      "Слишком много попыток, попробуйте через 30 секунд",
    );
  });

  it("asks for the listing as the one carrier of its own refusal", async () => {
    mockGetTopics.mockResolvedValue({ data: null, error: refusal(500) });

    await openBoard();

    // Two reads fill the listing, and the plate stays down over both.
    expect(mockGetTopics.mock.calls.map((call) => call[2])).toEqual([
      { ownsRefusal: true, ownsRateLimit: true },
      { ownsRefusal: true, ownsRateLimit: true },
    ]);
  });
});

// A topic nobody answered was last active when it was opened, as the server
// sorts the column; the name stands before the date, as on DM2.
describe("TopicsList last activity", () => {
  // Staff, whose name elsewhere carries the badge of the role: the cell leaves it out.
  const STAFF = "Moderator";
  const QUIET = {
    ...topic("t-quiet", "Никто не ответил", false),
    author: { username: "author", role: STAFF },
  };
  const GONE = {
    ...topic("t-gone", "Ответил удаленный", false),
    topicNumber: 4,
    commentsCount: 1,
    lastComment: {
      id: "c-2",
      createdUtc: "2026-07-06T12:00:00Z",
      author: null,
    },
  };
  const ANSWERED = {
    ...topic("t-answered", "Уже ответили", false),
    topicNumber: 3,
    commentsCount: 1,
    lastComment: {
      id: "c-1",
      createdUtc: "2026-07-05T12:00:00Z",
      author: { username: "reader", role: STAFF },
    },
  };

  beforeEach(() => {
    freshPage();
    answerBoard([], [QUIET, ANSWERED, GONE]);
  });

  const cell = (wrapper: VueWrapper, row: { title: string }) =>
    rowOf(wrapper, row).get("td.col-lastActivity");

  /** The date as the cell writes it, on the site's clock. */
  const dated = (utc: string) => formatSiteTime(utc, DATE_TIME_FORMAT);

  it("dates a topic nobody has answered by its opening, linked to the topic", async () => {
    const wrapper = await openBoard();

    const quiet = cell(wrapper, QUIET);
    expect(quiet.text()).not.toContain(VALUE_UNAVAILABLE);
    const date = quiet.get("a.last-activity-link");
    expect(date.text()).toBe(dated(QUIET.createdUtc));
    expect(date.attributes("href")).toBe("/forum/main/2");
  });

  it("keeps dating an answered topic by its last comment", async () => {
    const wrapper = await openBoard();

    const date = cell(wrapper, ANSWERED).get("a.last-activity-link");
    expect(date.text()).toBe(dated(ANSWERED.lastComment.createdUtc));
    expect(date.attributes("href")).toBe("/forum/main/3#comment-c-1");
  });

  /** The name, the comma, then the date: the cell as a reader reads it. */
  const reads = (wrapper: VueWrapper, row: { title: string }) => {
    const one = cell(wrapper, row);
    return {
      name: one.find(".user-link"),
      // The spaces of the markup condensed; the no-break space of the date stays.
      text: one.text().replace(/[\t\n\r ]+/g, " "),
      // The badge of the role a staff name carries elsewhere is left out here.
      badge: one.find(".role-badge").exists(),
    };
  };

  it("names who wrote last before the date of an answered topic", async () => {
    const wrapper = await openBoard();

    const answered = reads(wrapper, ANSWERED);
    expect(answered.name.text()).toBe("reader");
    expect(answered.name.attributes("href")).toBe("/users/reader");
    expect(answered.badge).toBe(false);
    expect(answered.text).toBe(
      `reader, ${dated(ANSWERED.lastComment.createdUtc)}`,
    );
  });

  it("names the author of a topic nobody has answered", async () => {
    const wrapper = await openBoard();

    const quiet = reads(wrapper, QUIET);
    expect(quiet.name.text()).toBe(QUIET.author.username);
    expect(quiet.badge).toBe(false);
    expect(quiet.text).toBe(`author, ${dated(QUIET.createdUtc)}`);
  });

  it("leaves the role out of the author column as well", async () => {
    const wrapper = await openBoard();

    const author = rowOf(wrapper, QUIET).get("td.col-author");
    expect(author.get(".user-link").text()).toBe(QUIET.author.username);
    expect(author.find(".role-badge").exists()).toBe(false);
  });

  it("says a removed account where the last one to write is gone", async () => {
    const wrapper = await openBoard();

    const gone = reads(wrapper, GONE);
    expect(gone.name.exists()).toBe(false);
    expect(gone.text).toBe(
      `Аккаунт удален, ${dated(GONE.lastComment.createdUtc)}`,
    );
  });
});

// Every column stands, and the header of the sorted one announces it (aria-sort).
describe("TopicsList sorted column", () => {
  const EVERY = [
    "Топик",
    "Автор",
    "Комментарии",
    "Нравится",
    "Дата создания",
    "Последняя активность",
  ];
  beforeEach(() => {
    freshPage();
    answerBoard([], [PLAIN]);
  });

  /** The listing sorted as the address says. */
  const sortedBy = (query = "") => openBoard(`/forum/main${query}`);

  /** The labels of the headers of the table. */
  const standing = (wrapper: VueWrapper) =>
    wrapper.findAll("thead th").map((th) => th.text());

  it("stands every column, sorted by the last activity by default", async () => {
    const wrapper = await sortedBy();

    expect(standing(wrapper)).toEqual(EVERY);
    expect(
      wrapper.get("thead th.col-lastActivity").attributes("aria-sort"),
    ).toBe("descending");
  });

  it("stands every column sorted by the date of creation", async () => {
    const wrapper = await sortedBy("?sortBy=created&sortOrder=asc");

    expect(standing(wrapper)).toEqual(EVERY);
    expect(wrapper.get("thead th.col-created").attributes("aria-sort")).toBe(
      "ascending",
    );
  });

  it("stands every column sorted by the likes", async () => {
    const wrapper = await sortedBy("?sortBy=likes");

    expect(standing(wrapper)).toEqual(EVERY);
    expect(wrapper.get("thead th.col-likes").attributes("aria-sort")).toBe(
      "descending",
    );
  });
});
