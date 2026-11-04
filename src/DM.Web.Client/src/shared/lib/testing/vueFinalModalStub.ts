/**
 * vue-final-modal reduced to a content box with its class and default slot.
 *
 *   vi.mock("vue-final-modal", async () =>
 *     (await import("@/shared/lib/testing/vueFinalModalStub")).vueFinalModal(),
 *   );
 */
import { defineComponent, h } from "vue";

export const VueFinalModalStub = defineComponent({
  props: { contentClass: { type: String, default: "" } },
  setup:
    (props, { slots }) =>
    () =>
      h(
        "div",
        { class: ["vfm-content", props.contentClass] },
        slots.default?.(),
      ),
});

/** The module vue-final-modal stands for under the stub. */
export const vueFinalModal = () => ({ VueFinalModal: VueFinalModalStub });
