/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import {
  enableAutoUnmount,
  flushPromises,
  mount,
  type VueWrapper,
} from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { useToast } from "@/shared/lib/composables/useToast";
import { BODY_TEXT_MAX_LENGTH } from "@/shared/lib/constants/content";
import CommentComposer from "./CommentComposer.vue";

const editor = vi.hoisted(() => ({ cleared: 0 }));

vi.mock("@/shared/ui/BBCodeEditor", () => ({
  BBCodeEditor: {
    name: "BBCodeEditor",
    props: ["modelValue", "maxLength", "draftKey", "disabled", "isModerator"],
    emits: ["update:modelValue", "submit"],
    methods: {
      clear() {
        editor.cleared += 1;
      },
    },
    template:
      "<textarea class='editor-stub' :value='modelValue' @input='$emit(\"update:modelValue\", $event.target.value)' />",
  },
}));

enableAutoUnmount(afterEach);

let send: ReturnType<typeof vi.fn>;

beforeEach(() => {
  editor.cleared = 0;
  localStorage.clear();
  setActivePinia(createPinia());
  send = vi.fn().mockResolvedValue({ error: null });
  const { toasts, dismiss } = useToast();
  [...toasts.value].forEach((t) => dismiss(t.id));
});

const render = (extra: Record<string, unknown> = {}) =>
  mount(CommentComposer, {
    props: { send, draftKey: "game:the-game:comment", ...extra } as never,
  });

const editorOf = (wrapper: VueWrapper) =>
  wrapper.getComponent({ name: "BBCodeEditor" });

const field = (wrapper: VueWrapper) =>
  wrapper.get(".editor-stub").element as HTMLTextAreaElement;

const refusal = (reason: string) => ({
  error: {
    type: "",
    title: "",
    status: 400,
    traceId: "t",
    errors: { Text: [reason] },
  },
});

describe("the counter of the form", () => {
  it("counts to what the server takes for a comment", () => {
    expect(editorOf(render()).props("maxLength")).toBe(BODY_TEXT_MAX_LENGTH);
  });

  it("keeps the draft under the key it is given", () => {
    expect(editorOf(render()).props("draftKey")).toBe("game:the-game:comment");
  });
});

describe("sending a comment", () => {
  it("hands the text over, empties the field and drops the draft once it landed", async () => {
    let answer: (value: { error: null }) => void = () => {};
    send.mockReturnValueOnce(new Promise((resolve) => (answer = resolve)));
    const wrapper = render();

    await wrapper.get(".editor-stub").setValue("Вот ссылка");
    await wrapper.get("button").trigger("click");

    expect(send).toHaveBeenCalledWith("Вот ссылка");
    expect(field(wrapper).value).toBe("");
    expect(editor.cleared).toBe(0);

    answer({ error: null });
    await flushPromises();

    expect(editor.cleared).toBe(1);
    expect(wrapper.find(".action-result").exists()).toBe(false);
  });

  it("sends on the editor's own shortcut as on the button", async () => {
    const wrapper = render();

    await wrapper.get(".editor-stub").setValue("Ctrl+Enter");
    editorOf(wrapper).vm.$emit("submit");
    await flushPromises();

    expect(send).toHaveBeenCalledWith("Ctrl+Enter");
  });

  it("gives a refused text back with the server's reason under the button", async () => {
    const reason =
      "Скрытый блок работает только в игровых постах: [private]. Для примера оберните его в [noparse].";
    send.mockResolvedValueOnce(refusal(reason));
    const wrapper = render();

    await wrapper.get(".editor-stub").setValue("[private]секрет[/private]");
    await wrapper.get("button").trigger("click");
    await flushPromises();

    expect(field(wrapper).value).toBe("[private]секрет[/private]");
    expect(wrapper.get(".comment-composer__refusal").text()).toBe(reason);
    expect(useToast().toasts.value).toEqual([]);
    expect(editor.cleared).toBe(0);
  });

  it("says its own words where the server said none", async () => {
    send.mockResolvedValueOnce({
      error: { type: "", title: "", status: 500, traceId: "t" },
    });
    const wrapper = render();

    await wrapper.get(".editor-stub").setValue("Текст");
    await wrapper.get("button").trigger("click");
    await flushPromises();

    expect(wrapper.text()).toContain("Не удалось отправить комментарий");
  });

  it("sends nothing blank, and offers no press for it", async () => {
    const wrapper = render();

    await wrapper.get(".editor-stub").setValue("   ");

    expect(wrapper.get("button").attributes("disabled")).toBeDefined();
    editorOf(wrapper).vm.$emit("submit");
    await flushPromises();
    expect(send).not.toHaveBeenCalled();
  });

  it("sends one comment for two quick presses", async () => {
    send.mockReturnValueOnce(new Promise(() => {}));
    const wrapper = render();

    await wrapper.get(".editor-stub").setValue("Раз");
    editorOf(wrapper).vm.$emit("submit");
    editorOf(wrapper).vm.$emit("submit");
    await flushPromises();

    expect(send).toHaveBeenCalledTimes(1);
  });
});
