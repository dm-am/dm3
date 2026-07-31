/**
 * @vitest-environment jsdom
 */

/**
 * A refused send used to take the message with it. The field was emptied and
 * the editor cleared before the request went out, and the store answered with
 * the created message or with null — a shape that says nothing about why there
 * is no message. So a 400 on length, a blacklisted recipient or a dropped
 * connection all ended the same way: an empty field, an empty correspondence,
 * and no draft in localStorage either, because clear() removes that too.
 *
 * These pin the bargain the composer offers instead. The field empties at once
 * — that is what makes sending feel instant — and everything is handed back
 * when the send does not land: the text, the draft, and a sentence saying so.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { defineComponent, h, nextTick } from "vue";
import { flushPromises, mount, enableAutoUnmount } from "@vue/test-utils";
import { createPinia, setActivePinia, type Pinia } from "pinia";
import { useAuthStore } from "@/shared/stores";
import { useToast } from "@/shared/lib/composables/useToast";

const { api, clearSpy } = vi.hoisted(() => ({
  api: {
    getChat: vi.fn(),
    getMessages: vi.fn(),
    markAsRead: vi.fn(),
    sendMessage: vi.fn(),
    getMessagesBefore: vi.fn(),
    getMessageForEdit: vi.fn(),
  },
  clearSpy: vi.fn(),
}));

vi.mock("@/entities/message/api/messagingApi", () => ({ default: api }));

/**
 * Stands in for the editor: a plain textarea bound to the same v-model, plus
 * the clear() the page calls. Mounting the real one would drag tiptap in, and
 * the assertions here are about which of the two the page calls and when.
 */
vi.mock("@/shared/ui/BBCodeEditor", () => ({
  BBCodeEditor: defineComponent({
    name: "BBCodeEditor",
    props: { modelValue: { type: String, default: "" } },
    emits: ["update:modelValue", "submit"],
    setup(props, { emit, expose }) {
      expose({ clear: clearSpy, focus: vi.fn(), clearDraft: vi.fn() });
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

vi.mock("vue-router", () => ({
  useRoute: () => ({ params: { id: "c1" }, query: {} }),
  useRouter: () => ({ push: vi.fn() }),
}));

import ChatView from "./ChatView.vue";

enableAutoUnmount(afterEach);

const problem = (status: number, title = "") => ({
  type: "",
  title,
  status,
  traceId: "t",
});

const field = (wrapper: ReturnType<typeof mount>) =>
  wrapper.find(".editor-stub").element as HTMLTextAreaElement;

let pinia: Pinia;

async function openChat() {
  const wrapper = mount(ChatView, {
    global: { plugins: [pinia], stubs: { Teleport: true, RouterLink: true } },
  });
  await flushPromises();
  return wrapper;
}

describe("ChatView, sending a message", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    // jsdom has neither, and the message list is virtualized behind both.
    vi.stubGlobal(
      "ResizeObserver",
      class {
        observe() {}
        unobserve() {}
        disconnect() {}
      },
    );
    vi.stubGlobal(
      "IntersectionObserver",
      class {
        observe() {}
        unobserve() {}
        disconnect() {}
      },
    );
    localStorage.clear();
    pinia = createPinia();
    setActivePinia(pinia);
    const { toasts, dismiss } = useToast();
    [...toasts.value].forEach((t) => dismiss(t.id));
    useAuthStore().updateUser({ username: "Alice" } as never);
    api.getChat.mockResolvedValue({
      data: { id: "c1", participants: [] },
      error: null,
    });
    api.getMessages.mockResolvedValue({
      data: {
        resources: [],
        paging: {
          prevCursor: null,
          nextCursor: null,
          hasPrev: false,
          hasNext: false,
        },
      },
      error: null,
    });
    api.markAsRead.mockResolvedValue({ data: null, error: null });
  });

  it("keeps the text, the draft and names the reason when the send fails", async () => {
    api.sendMessage.mockResolvedValue({ data: null, error: problem(400) });
    const wrapper = await openChat();

    await wrapper.find(".editor-stub").setValue("длинное письмо");
    await wrapper.find(".send-button").trigger("click");
    await flushPromises();
    await nextTick();

    expect(api.sendMessage).toHaveBeenCalledWith("c1", "длинное письмо");
    expect(field(wrapper).value).toBe("длинное письмо");
    // clear() drops the saved draft, so it may not run before the send lands:
    // the draft is the only copy that outlives the tab.
    expect(clearSpy).not.toHaveBeenCalled();
    expect(useToast().toasts.value.map((t) => t.message)).toContain(
      "Не удалось отправить сообщение",
    );
  });

  it("empties the field and drops the draft once the send lands", async () => {
    api.sendMessage.mockResolvedValue({
      data: {
        id: "m1",
        text: "привет",
        createdUtc: "2026-07-01T10:00:00Z",
        likes: [],
      },
      error: null,
    });
    const wrapper = await openChat();

    await wrapper.find(".editor-stub").setValue("привет");
    await wrapper.find(".send-button").trigger("click");
    await flushPromises();
    await nextTick();

    expect(field(wrapper).value).toBe("");
    expect(clearSpy).toHaveBeenCalledTimes(1);
    expect(useToast().toasts.value).toHaveLength(0);
  });

  it("sends nothing on a field holding only spaces", async () => {
    const wrapper = await openChat();

    await wrapper.find(".editor-stub").setValue("   ");
    // Through the editor's own submit (Ctrl+Enter), which the disabled send
    // button would otherwise hide: the guard is in the handler.
    wrapper.findComponent({ name: "BBCodeEditor" }).vm.$emit("submit");
    await flushPromises();

    expect(api.sendMessage).not.toHaveBeenCalled();
    expect(field(wrapper).value).toBe("   ");
    expect(useToast().toasts.value).toHaveLength(0);
  });
});
