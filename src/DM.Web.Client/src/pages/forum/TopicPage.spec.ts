/**
 * @vitest-environment jsdom
 */

/**
 * A refused comment on a topic said nothing.
 *
 * The interceptor speaks for a 401, a 403, a 429, a 5xx and a lost connection,
 * and leaves a 400 to the page: that is the refusal that names what is wrong
 * with the text, a hidden block outside a game post among them. The topic
 * handed the text back into the field and stopped there, so the reader pressed
 * the button, saw the text return and learnt nothing. The chat composer and
 * the comment editor say it through notifyFailure, and so does this one now.
 */
import { describe, it, expect, beforeEach, vi } from "vitest";
import { defineComponent, h } from "vue";
import { mount, flushPromises } from "@vue/test-utils";
import { createPinia, setActivePinia, type Pinia } from "pinia";
import type { BadRequestError } from "@/shared/api/models/common";
import { useAuthStore } from "@/shared/stores";
import { useToast } from "@/shared/lib/composables/useToast";

const { api, clearSpy } = vi.hoisted(() => ({
  api: {
    getTopicByNumber: vi.fn(),
    getTopic: vi.fn(),
    getBoard: vi.fn(),
    getComments: vi.fn(),
    createComment: vi.fn(),
    markTopicAsRead: vi.fn(),
  },
  clearSpy: vi.fn(),
}));

vi.mock("@/entities/forum/api/forumApi", () => ({ default: api }));

vi.mock("vue-router", () => ({
  useRoute: () => ({ params: { alias: "main", num: "1" }, query: {} }),
  useRouter: () => ({
    push: vi.fn(),
    replace: vi.fn(),
    resolve: () => ({ href: "" }),
  }),
  RouterLink: { template: "<a><slot /></a>" },
}));

vi.mock("vue-final-modal", () => ({
  useModal: () => ({ open: vi.fn(), close: vi.fn() }),
}));

// The topic card, the filter bar and the moderator dialog each have a spec of
// their own; here only the composer under them is read.
vi.mock("@/features/topic", () => ({
  TopicView: { template: "<div class='topic-stub' />" },
}));

vi.mock("@/features/comment-filter", async () => {
  const { ref } = await import("vue");
  return {
    CommentsFilter: { template: "<div class='filter-stub' />" },
    useCommentsFilter: () => ({ searchParams: ref({}) }),
  };
});

vi.mock("@/features/auth", () => ({
  LoginPrompt: { template: "<div class='login-prompt' />" },
}));

vi.mock("@/features/moderation-actions", () => ({
  WarningDialog: { template: "<div />" },
}));

// A plain textarea bound to the same v-model, plus the clear() the page calls
// once a send lands: the real editor would drag tiptap in.
vi.mock("@/shared/ui/BBCodeEditor", () => ({
  BBCodeEditor: defineComponent({
    name: "BBCodeEditor",
    props: { modelValue: { type: String, default: "" } },
    emits: ["update:modelValue", "submit"],
    setup(props, { emit, expose }) {
      expose({ clear: clearSpy });
      return () =>
        h("textarea", {
          class: "editor-stub",
          value: props.modelValue,
          onInput: (e: Event) =>
            emit("update:modelValue", (e.target as HTMLTextAreaElement).value),
        });
    },
  }),
}));

import TopicPage from "./TopicPage.vue";

const REFUSAL =
  'Скрытый текст работает только в игровом тексте поста: [private="Вася"], символ 1. ' +
  "Чтобы показать разметку примером, поставьте ее между [noparse] и [/noparse].";

const refused = (): BadRequestError => ({
  type: "",
  title: "One or more validation errors occurred.",
  status: 400,
  traceId: "t",
  errors: { Text: [REFUSAL] },
});

const messages = () => useToast().toasts.value.map((t) => t.message);

let pinia: Pinia;

async function openTopic() {
  const wrapper = mount(TopicPage, {
    global: {
      plugins: [pinia],
      stubs: {
        RouterView: true,
        "router-view": true,
        SecondaryText: {
          template: "<div class='secondary-text'><slot /></div>",
        },
      },
    },
  });
  await flushPromises();
  return wrapper;
}

async function send(
  wrapper: Awaited<ReturnType<typeof openTopic>>,
  text: string,
) {
  await wrapper.get(".editor-stub").setValue(text);
  await wrapper.get(".comment-send-button").trigger("click");
  await flushPromises();
}

describe("TopicPage, sending a comment", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    pinia = createPinia();
    setActivePinia(pinia);
    const { toasts, dismiss } = useToast();
    [...toasts.value].forEach((t) => dismiss(t.id));
    useAuthStore().updateUser({ username: "Alice" } as never);
    api.getTopicByNumber.mockResolvedValue({
      data: {
        resource: {
          id: "t-1",
          title: "Тема",
          isClosed: false,
          commentsCount: 0,
          unreadCommentsCount: 0,
          board: { id: "b-1", alias: "main", title: "Общий" },
          viewer: { canComment: true },
        },
      },
      error: null,
    });
    api.getComments.mockResolvedValue({
      data: { resources: [], paging: null },
      error: null,
    });
    api.markTopicAsRead.mockResolvedValue({ data: null, error: null });
  });

  it("names the server's reason and keeps the text when the send is refused", async () => {
    api.createComment.mockResolvedValue({ data: null, error: refused() });
    const wrapper = await openTopic();

    await send(wrapper, '[private="Вася"]секрет[/private]');

    // Under the "Отправить" that sent it, and in no plate.
    expect(wrapper.get(".comment-send-refusal").text()).toBe(REFUSAL);
    expect(messages()).toEqual([]);
    expect(
      (wrapper.get(".editor-stub").element as HTMLTextAreaElement).value,
    ).toBe('[private="Вася"]секрет[/private]');
    expect(clearSpy).not.toHaveBeenCalled();
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
    expect(clearSpy).toHaveBeenCalledOnce();
  });
});

/**
 * A closed topic is closed to everybody's comments and open to everybody's
 * reading. What was written stays, and so does the discussion of a closed
 * topic nobody answered: the filter bar and "Комментариев пока нет". Where the
 * composer would be stands the plaque of a refusal, "Топик закрыт для
 * комментариев", for a guest too, ahead of the way in, which would open
 * nothing (the owner, 12.10: he may read, he just may not write).
 */
describe("TopicPage, a closed topic", () => {
  function topicOf(over: Record<string, unknown>) {
    api.getTopicByNumber.mockResolvedValue({
      data: {
        resource: {
          id: "t-1",
          title: "Тема",
          isClosed: true,
          commentsCount: 3,
          unreadCommentsCount: 0,
          board: { id: "b-1", alias: "main", title: "Общий" },
          ...over,
        },
      },
      error: null,
    });
  }

  beforeEach(() => {
    vi.clearAllMocks();
    pinia = createPinia();
    setActivePinia(pinia);
    useAuthStore().updateUser({ username: "Alice" } as never);
    api.getComments.mockResolvedValue({
      data: { resources: [], paging: null },
      error: null,
    });
    api.markTopicAsRead.mockResolvedValue({ data: null, error: null });
  });

  it("says the topic is closed where the composer would be", async () => {
    topicOf({});
    const wrapper = await openTopic();

    expect(wrapper.find(".topic-stub").exists()).toBe(true);
    expect(wrapper.find(".editor-stub").exists()).toBe(false);
    expect(wrapper.get(".comment-closed-hint").text()).toBe(
      "Топик закрыт для комментариев",
    );
    // The comments it has are still there to read.
    expect(wrapper.find(".filter-stub").exists()).toBe(true);
    expect(wrapper.html()).toContain("router-view-stub");
  });

  it("tells a guest the topic is closed instead of inviting him in", async () => {
    useAuthStore().updateUser(null);
    topicOf({});
    const wrapper = await openTopic();

    expect(wrapper.find(".login-prompt").exists()).toBe(false);
    expect(wrapper.get(".comment-closed-hint").text()).toBe(
      "Топик закрыт для комментариев",
    );
  });

  it("invites a guest in under an open topic", async () => {
    useAuthStore().updateUser(null);
    topicOf({ isClosed: false });
    const wrapper = await openTopic();

    expect(wrapper.find(".login-prompt").exists()).toBe(true);
  });

  // Under an open topic the topic's answer about the viewer decides, and a
  // ban is nothing else on the page can see. The page used to offer the form
  // to a banned reader and print the refusal under it.
  it("leaves nothing under an open topic that takes no comment from the reader", async () => {
    topicOf({ isClosed: false, viewer: { canComment: false } });
    const wrapper = await openTopic();

    expect(wrapper.find(".editor-stub").exists()).toBe(false);
    expect(wrapper.find(".login-prompt").exists()).toBe(false);
    expect(wrapper.find(".comment-closed-hint").exists()).toBe(false);
    expect(wrapper.find(".comment-input-wrapper").exists()).toBe(false);
  });

  // An answer that did not come is no "yes".
  it("offers no form under a topic that says nothing about the viewer", async () => {
    topicOf({ isClosed: false });
    const wrapper = await openTopic();

    expect(wrapper.find(".editor-stub").exists()).toBe(false);
  });

  // The answer is about whoever held the session when the topic was read: a
  // session that has just ended is offered the way in and not the form, and
  // the guest who signs in from the prompt is asked about again.
  it("reads the topic again in place when the viewer changes", async () => {
    const answered = (canComment: boolean) => ({
      data: {
        resource: {
          id: "t-1",
          title: "Тема",
          isClosed: false,
          commentsCount: 3,
          unreadCommentsCount: 0,
          board: { id: "b-1", alias: "main", title: "Общий" },
          viewer: { canComment },
        },
      },
      error: null,
    });
    topicOf({ isClosed: false, viewer: { canComment: true } });
    api.getBoard.mockResolvedValue({
      data: { resource: { id: "b-1", alias: "main", title: "Общий" } },
      error: null,
    });
    const wrapper = await openTopic();
    expect(wrapper.find(".editor-stub").exists()).toBe(true);

    // Signed out: no form on the answer given to the session that ended, the
    // way in instead, and the topic is asked about the guest.
    api.getTopic.mockResolvedValue(answered(false));
    useAuthStore().updateUser(null);
    await flushPromises();
    expect(api.getTopic).toHaveBeenCalledWith("t-1");
    expect(wrapper.find(".editor-stub").exists()).toBe(false);
    expect(wrapper.find(".login-prompt").exists()).toBe(true);

    // Signed in again from the prompt: asked again, and the form follows.
    api.getTopic.mockClear();
    api.getTopic.mockResolvedValue(answered(true));
    useAuthStore().updateUser({ username: "Alice" } as never);
    await flushPromises();

    expect(api.getTopic).toHaveBeenCalledWith("t-1");
    expect(wrapper.find(".editor-stub").exists()).toBe(true);
    expect(wrapper.find(".login-prompt").exists()).toBe(false);
  });

  it("keeps the discussion of a closed topic without comments", async () => {
    topicOf({ commentsCount: 0 });
    const wrapper = await openTopic();

    expect(wrapper.find(".topic-stub").exists()).toBe(true);
    expect(wrapper.find(".filter-stub").exists()).toBe(true);
    expect(wrapper.html()).toContain("router-view-stub");
    expect(wrapper.find(".comment-closed-hint").exists()).toBe(true);
  });
});
