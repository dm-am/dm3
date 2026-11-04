/**
 * @vitest-environment jsdom
 */

// The interceptor leaves a 400 to the page: the refusal that names what is
// wrong with the text, said under the button, with the text handed back.
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import {
  enableAutoUnmount,
  flushPromises,
  type VueWrapper,
} from "@vue/test-utils";
import { RouterView } from "vue-router";
import { createPinia, setActivePinia, type Pinia } from "pinia";
import type { BadRequestError } from "@/shared/api/models/common";
import { useAuthStore } from "@/shared/stores";
import { useToast } from "@/shared/lib/composables/useToast";
import { composerDraftKey } from "@/shared/lib/utils/draftKey";
import { mountPage } from "@/shared/lib/testing/mountPage";
import TopicPage from "./TopicPage.vue";

const api = vi.hoisted(() => ({
  getTopicByNumber: vi.fn(),
  getTopic: vi.fn(),
  getBoard: vi.fn(),
  getComments: vi.fn(),
  createComment: vi.fn(),
  markTopicAsRead: vi.fn(),
}));

// The network only: the card, the filter, the editor and the way in are the real ones.
vi.mock("@/entities/forum/api/forumApi", () => ({ default: api }));

const REFUSAL =
  'Скрытый блок работает только в игровых постах: [private="Вася"]. Для примера оберните его в [noparse].';

const refused = (): BadRequestError => ({
  type: "",
  title: "One or more validation errors occurred.",
  status: 400,
  traceId: "t",
  errors: { Text: [REFUSAL] },
});

/** The topic as the server answers it, with what the reader may do. */
const topicOf = (over: Record<string, unknown> = {}) => ({
  id: "t-1",
  topicNumber: 1,
  author: {
    username: "SolohinLex",
    role: "RegularUser",
    lastActivityUtc: null,
  },
  createdUtc: "2026-10-01T10:00:00Z",
  modifiedUtc: null,
  title: "Тема",
  description: "<p>Описание</p>",
  isAttached: false,
  attachOrder: null,
  isClosed: false,
  closedByAuthor: false,
  lastActivityUtc: "2026-10-01T10:00:00Z",
  lastComment: null,
  commentsCount: 0,
  unreadCommentsCount: 0,
  board: { id: "b-1", alias: "main", title: "Общий", moderators: [] },
  likes: [],
  likesCount: 0,
  ...over,
});

const served = (over: Record<string, unknown> = {}) => ({
  data: { resource: topicOf(over) },
  error: null,
});

const messages = () => useToast().toasts.value.map((t) => t.message);

let pinia: Pinia;

enableAutoUnmount(afterEach);

async function openTopic() {
  const { wrapper } = await mountPage(TopicPage, "/forum/main/1", {
    pinia,
    routes: [
      ["topic", "/forum/:alias/:num"],
      ["topic-unread", "/forum/:alias/:num/unread"],
      ["forum", "/forum/:alias"],
      ["profile", "/users/:username"],
      ["site-statistics", "/statistics"],
    ],
  });
  await flushPromises();
  return wrapper;
}

/** The source of the markup editor, where a comment is typed as BBCode. */
async function sourceField(wrapper: VueWrapper) {
  await wrapper.get('.mode-tab[aria-label="Режим BBCode"]').trigger("click");
  return wrapper.get<HTMLTextAreaElement>(".bbcode-textarea");
}

const composer = (wrapper: VueWrapper) => wrapper.find(".bbcode-editor");

async function send(wrapper: VueWrapper, text: string) {
  await (await sourceField(wrapper)).setValue(text);
  await wrapper.get(".comment-send-button").trigger("click");
  await flushPromises();
}

/** Whether the editor keeps a draft of the comment typed under this topic. */
function keepsDraft(): boolean {
  const subject = composerDraftKey("topic", "comment", "t-1");
  for (let index = 0; index < localStorage.length; index++)
    if (localStorage.key(index)?.endsWith(subject)) return true;
  return false;
}

beforeEach(() => {
  vi.clearAllMocks();
  localStorage.clear();
  pinia = createPinia();
  setActivePinia(pinia);
  const { toasts, dismiss } = useToast();
  [...toasts.value].forEach((t) => dismiss(t.id));
  useAuthStore().updateUser({ username: "Alice" } as never);
  api.getComments.mockResolvedValue({
    data: { resources: [], paging: null },
    error: null,
  });
  api.markTopicAsRead.mockResolvedValue({ data: null, error: null });
});

describe("TopicPage, sending a comment", () => {
  beforeEach(() => {
    api.getTopicByNumber.mockResolvedValue(
      served({ viewer: { canComment: true } }),
    );
  });

  it("names the server's reason and keeps the text when the send is refused", async () => {
    api.createComment.mockResolvedValue({ data: null, error: refused() });
    const wrapper = await openTopic();

    await send(wrapper, '[private="Вася"]секрет[/private]');

    // Under the "Отправить" that sent it, and in no plate.
    expect(wrapper.get(".comment-send-refusal").text()).toBe(REFUSAL);
    expect(messages()).toEqual([]);
    expect(
      wrapper.get<HTMLTextAreaElement>(".bbcode-textarea").element.value,
    ).toBe('[private="Вася"]секрет[/private]');
    // The draft stays until a send lands.
    expect(keepsDraft()).toBe(true);
  });

  it("says nothing of its own over a refusal the interceptor already named", async () => {
    api.createComment.mockResolvedValue({
      data: null,
      error: {
        type: "",
        title: "Аккаунт заблокирован",
        status: 403,
        traceId: "t",
      },
    });
    const wrapper = await openTopic();

    await send(wrapper, "текст");

    expect(messages()).toEqual([]);
  });

  it("clears the editor and says nothing once the comment lands", async () => {
    api.createComment.mockResolvedValue({
      data: { resource: { id: "c-1", text: "<p>текст</p>" } },
      error: null,
    });
    const wrapper = await openTopic();

    await send(wrapper, "текст");

    expect(messages()).toEqual([]);
    expect(
      wrapper.get<HTMLTextAreaElement>(".bbcode-textarea").element.value,
    ).toBe("");
    expect(keepsDraft()).toBe(false);
  });
});

// A closed topic: closed to everybody's comments, a guest's included, open
// to everybody's reading; the plaque of a refusal stands where the composer would.
describe("TopicPage, a closed topic", () => {
  const closedTopic = (over: Record<string, unknown>) =>
    api.getTopicByNumber.mockResolvedValue(
      served({ isClosed: true, commentsCount: 3, ...over }),
    );

  it("says the topic is closed where the composer would be", async () => {
    closedTopic({});
    const wrapper = await openTopic();

    expect(wrapper.get(".topic-title").text()).toContain("Тема");
    expect(composer(wrapper).exists()).toBe(false);
    expect(wrapper.get(".comment-closed-hint").text()).toBe(
      "Топик закрыт для комментариев",
    );
    // The comments it has are still there to read.
    expect(wrapper.find(".comments-filter").exists()).toBe(true);
    expect(wrapper.findComponent(RouterView).exists()).toBe(true);
  });

  it("tells a guest the topic is closed instead of inviting him in", async () => {
    useAuthStore().updateUser(null);
    closedTopic({});
    const wrapper = await openTopic();

    expect(wrapper.find(".login-prompt").exists()).toBe(false);
    expect(wrapper.get(".comment-closed-hint").text()).toBe(
      "Топик закрыт для комментариев",
    );
  });

  it("invites a guest in under an open topic", async () => {
    useAuthStore().updateUser(null);
    closedTopic({ isClosed: false });
    const wrapper = await openTopic();

    expect(wrapper.find(".login-prompt").exists()).toBe(true);
  });

  // Under an open topic the topic's answer about the viewer decides.
  it("leaves nothing under an open topic that takes no comment from the reader", async () => {
    closedTopic({ isClosed: false, viewer: { canComment: false } });
    const wrapper = await openTopic();

    expect(composer(wrapper).exists()).toBe(false);
    expect(wrapper.find(".login-prompt").exists()).toBe(false);
    expect(wrapper.find(".comment-closed-hint").exists()).toBe(false);
    expect(wrapper.find(".comment-input-wrapper").exists()).toBe(false);
  });

  // No answer is no "yes".
  it("offers no form under a topic that says nothing about the viewer", async () => {
    closedTopic({ isClosed: false });
    const wrapper = await openTopic();

    expect(composer(wrapper).exists()).toBe(false);
  });

  // The answer is about whoever held the session when the topic was read.
  it("reads the topic again in place when the viewer changes", async () => {
    const answered = (canComment: boolean) =>
      served({ commentsCount: 3, viewer: { canComment } });
    closedTopic({ isClosed: false, viewer: { canComment: true } });
    api.getBoard.mockResolvedValue({
      data: { resource: { id: "b-1", alias: "main", title: "Общий" } },
      error: null,
    });
    const wrapper = await openTopic();
    expect(composer(wrapper).exists()).toBe(true);

    // Signed out: the way in, and the topic asked about the guest.
    api.getTopic.mockResolvedValue(answered(false));
    useAuthStore().updateUser(null);
    await flushPromises();
    expect(api.getTopic).toHaveBeenCalledWith("t-1");
    expect(composer(wrapper).exists()).toBe(false);
    expect(wrapper.find(".login-prompt").exists()).toBe(true);

    // Signed in again: asked again, and the form follows.
    api.getTopic.mockClear();
    api.getTopic.mockResolvedValue(answered(true));
    useAuthStore().updateUser({ username: "Alice" } as never);
    await flushPromises();

    expect(api.getTopic).toHaveBeenCalledWith("t-1");
    expect(composer(wrapper).exists()).toBe(true);
    expect(wrapper.find(".login-prompt").exists()).toBe(false);
  });

  it("keeps the discussion of a closed topic without comments", async () => {
    closedTopic({ commentsCount: 0 });
    const wrapper = await openTopic();

    expect(wrapper.get(".topic-title").text()).toContain("Тема");
    expect(wrapper.find(".comments-filter").exists()).toBe(true);
    expect(wrapper.findComponent(RouterView).exists()).toBe(true);
    expect(wrapper.find(".comment-closed-hint").exists()).toBe(true);
  });
});
