/**
 * The markup editor reduced to what a form reads: a text area showing the
 * value and the props a form passes.
 *
 *   vi.mock("@/shared/ui/BBCodeEditor", async () =>
 *     (await import("@/shared/lib/testing/bbcodeEditorStub")).bbcodeEditor(),
 *   );
 */
import { defineComponent, h } from "vue";
import { vi } from "vitest";

// A render function, not `template`: the test build runs the runtime-only Vue.
const BBCodeEditorStub = defineComponent({
  name: "BBCodeEditor",
  props: {
    modelValue: { type: String, default: "" },
    context: { type: String, default: undefined },
    checkContext: { type: String, default: undefined },
    maxLength: { type: Number, default: 0 },
  },
  setup(props) {
    return () =>
      h("textarea", { class: "editor-stub", value: props.modelValue });
  },
});

/** The module @/shared/ui/BBCodeEditor stands for under the stub. */
export const bbcodeEditor = () => ({ BBCodeEditor: BBCodeEditorStub });

/**
 * The editor of a composer: the same text area on the v-model, with clear(),
 * focus() and clearDraft(); `clear` is the spec's spy.
 *
 *   vi.mock("@/shared/ui/BBCodeEditor", async () =>
 *     (await import("@/shared/lib/testing/bbcodeEditorStub")).bbcodeComposer(
 *       (await import("./X.doubles")).clearSpy,
 *     ),
 *   );
 */
export const bbcodeComposer = (clear: () => void) => ({
  BBCodeEditor: defineComponent({
    name: "BBCodeEditor",
    props: {
      modelValue: { type: String, default: "" },
      context: { type: String, default: undefined },
    },
    emits: ["update:modelValue", "submit"],
    setup(props, { emit, expose }) {
      expose({ clear, focus: vi.fn(), clearDraft: vi.fn() });
      return () =>
        h("textarea", {
          class: "editor-stub",
          value: props.modelValue,
          onInput: (e: Event) =>
            emit("update:modelValue", (e.target as HTMLTextAreaElement).value),
        });
    },
  }),
});

/**
 * The editor of a ticket answer: the text area on the v-model, the props the
 * answer form passes and clear().
 *
 *   vi.mock("@/shared/ui/BBCodeEditor", async () =>
 *     (await import("@/shared/lib/testing/bbcodeEditorStub")).bbcodeAnswer(),
 *   );
 */
export const bbcodeAnswer = () => ({
  BBCodeEditor: defineComponent({
    name: "BBCodeEditor",
    props: {
      modelValue: { type: String, default: "" },
      maxLength: { type: Number, default: 0 },
      draftKey: { type: String, default: undefined },
      disabled: { type: Boolean, default: false },
    },
    emits: ["update:modelValue", "submit"],
    setup(props, { emit, expose }) {
      expose({ clear: () => {} });
      return () =>
        h("textarea", {
          class: "editor-stub",
          value: props.modelValue,
          onInput: (e: Event) =>
            emit("update:modelValue", (e.target as HTMLTextAreaElement).value),
        });
    },
  }),
});
