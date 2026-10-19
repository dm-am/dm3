/**
 * @vitest-environment jsdom
 */

/**
 * "Создать топик" under the board listing (TopicCreateForm), as the listing
 * draws it: who is offered the button or the invitation, and what the form
 * says about a refused topic.
 */
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { flushPromises } from "@vue/test-utils";
import { useAuthStore } from "@/shared/stores";
import {
  mockGetTopics,
  mockCreateTopic,
  mockGetBoard,
} from "./TopicsList.doubles";
import {
  refusal,
  board,
  messages,
  mountList,
  unmountList,
  signedInReader,
} from "./TopicsList.fixtures";

vi.mock("@/entities/forum/api/forumApi", async () =>
  (await import("./TopicsList.doubles")).forumApi(),
);

vi.mock("vue-router", async () =>
  (await import("./TopicsList.doubles")).vueRouter(),
);

afterEach(unmountList);

// The form answers for what was typed and what the server names; an expired
// session and a dead connection stay with the interceptor.
describe("TopicsList create form", () => {
  beforeEach(() => {
    signedInReader();
    mockGetTopics.mockResolvedValue({
      data: { resources: [], paging: null },
      error: null,
    });
  });

  /** Opens the inline form and presses "Создать". */
  async function create(wrapper: Awaited<ReturnType<typeof mountList>>) {
    await wrapper.find(".create-topic-button").trigger("click");
    await wrapper.find("form.create-topic-form").trigger("submit");
    await flushPromises();
  }

  // About the reader, not the press: the plate, and the form says nothing.
  it.each([0, 401])(
    "leaves a failure of status %i to the interceptor",
    async (status) => {
      mockCreateTopic.mockResolvedValue({
        data: null,
        error: { ...refusal(status), title: "Раздел закрыт для записи" },
      });
      const wrapper = await mountList();

      await create(wrapper);

      expect(wrapper.find(".create-topic-error").exists()).toBe(false);
      // Nothing was created, so the form stays open.
      expect(wrapper.find("form.create-topic-form").exists()).toBe(true);
    },
  );

  // The board policy, a rate limit, a dead server stand in the form, over its buttons.
  it("says a failure of status 500 in the form", async () => {
    mockCreateTopic.mockResolvedValue({
      data: null,
      error: { ...refusal(500), title: "Раздел закрыт для записи" },
    });
    const wrapper = await mountList();

    await create(wrapper);

    expect(wrapper.get(".create-topic-error").text()).toBe(
      "Раздел закрыт для записи",
    );
    expect(wrapper.find("form.create-topic-form").exists()).toBe(true);
    expect(messages()).toEqual([]);
  });

  it("prints a refusal the interceptor says nothing about", async () => {
    mockCreateTopic.mockResolvedValue({
      data: null,
      error: { ...refusal(409), title: "Топик с таким заголовком уже есть" },
    });
    const wrapper = await mountList();

    await create(wrapper);

    expect(wrapper.get(".create-topic-error").text()).toBe(
      "Топик с таким заголовком уже есть",
    );
  });
});

// "Создать топик" by the board's answer for the viewer: the news and the
// contests take topics from moderation alone, and a guest is invited only
// where signing in opens one.
describe('TopicsList "Создать топик" by the board\'s answer', () => {
  const answering = (
    canCreateTopic: boolean,
    canCreateTopicAfterSignIn = false,
  ) => ({
    ...board,
    viewer: { canCreateTopic, canCreateTopicAfterSignIn },
  });

  function signIn(username: string | null) {
    localStorage.clear();
    if (username) {
      localStorage.setItem(
        "user",
        JSON.stringify({ username, role: "RegularUser" }),
      );
    }
  }

  beforeEach(() => {
    vi.clearAllMocks();
    mockGetTopics.mockResolvedValue({
      data: { resources: [], paging: null },
      error: null,
    });
  });

  it("offers the button to a member the board takes topics from", async () => {
    signIn("reader");
    const wrapper = await mountList({}, answering(true));

    expect(wrapper.find(".create-topic-button").exists()).toBe(true);
    expect(wrapper.find("login-prompt-stub").exists()).toBe(false);
  });

  it("offers a member nothing on a board of the moderation", async () => {
    signIn("reader");
    const wrapper = await mountList({}, answering(false));

    expect(wrapper.find(".create-topic-section").exists()).toBe(false);
    expect(wrapper.find(".create-topic-button").exists()).toBe(false);
    expect(wrapper.find("form.create-topic-form").exists()).toBe(false);
  });

  // The board of the list of boards carries no answer: nothing is offered on it.
  it("offers nothing until the board answers", async () => {
    signIn("reader");
    const { viewer: _unanswered, ...listed } = board;
    const wrapper = await mountList({}, listed);

    expect(wrapper.find(".create-topic-section").exists()).toBe(false);
  });

  it("invites a guest to sign in where a member may open a topic", async () => {
    signIn(null);
    const wrapper = await mountList({}, answering(false, true));

    expect(wrapper.find("login-prompt-stub").exists()).toBe(true);
    expect(wrapper.find(".create-topic-button").exists()).toBe(false);
  });

  it("invites a guest to nothing in the news", async () => {
    signIn(null);
    const wrapper = await mountList({}, answering(false, false));

    expect(wrapper.find(".create-topic-section").exists()).toBe(false);
  });

  // The answer is for whoever held the session at the read.
  it("reads the board again for the viewer who signed in", async () => {
    signIn(null);
    mockGetBoard.mockResolvedValue({
      data: { resource: answering(true) },
      error: null,
    });
    const wrapper = await mountList({}, answering(false, true));
    expect(wrapper.find("login-prompt-stub").exists()).toBe(true);

    useAuthStore().updateUser({
      username: "reader",
      role: "RegularUser",
    } as never);
    await flushPromises();

    // No refusal flags: this read shows nothing of a failure.
    expect(mockGetBoard).toHaveBeenCalledWith("main", undefined);
    expect(wrapper.find("login-prompt-stub").exists()).toBe(false);
    expect(wrapper.find(".create-topic-button").exists()).toBe(true);
  });
});
