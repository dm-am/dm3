/**
 * @vitest-environment jsdom
 */

import {
  describe,
  it,
  expect,
  vi,
  beforeEach,
  afterEach,
  type Mock,
} from "vitest";
import { mount } from "@vue/test-utils";
import { h, defineComponent } from "vue";
import SpoilerView from "./SpoilerView.vue";
import NsfwView from "./NsfwView.vue";

// Mock Tiptap's NodeViewWrapper and NodeViewContent
vi.mock("@tiptap/vue-3", () => ({
  NodeViewWrapper: defineComponent({
    name: "NodeViewWrapper",
    props: ["class"],
    setup(props, { slots }) {
      return () => h("div", { class: props.class }, slots.default?.());
    },
  }),
  NodeViewContent: defineComponent({
    name: "NodeViewContent",
    setup(_, { slots }) {
      return () => h("div", { class: "node-view-content" }, slots.default?.());
    },
  }),
  nodeViewProps: {
    node: {
      type: Object,
      default: () => ({ attrs: {} }),
    },
    updateAttributes: {
      type: Function,
      default: () => {},
    },
    deleteNode: {
      type: Function,
      default: () => {},
    },
    selected: {
      type: Boolean,
      default: false,
    },
    extension: {
      type: Object,
      default: () => ({}),
    },
    getPos: {
      type: Function,
      default: () => 0,
    },
    editor: {
      type: Object,
      default: () => ({}),
    },
    decorations: {
      type: Array,
      default: () => [],
    },
  },
}));

// Create mock node with attrs - use 'as any' to bypass Tiptap's Node interface
function createMockNode(attrs: Record<string, unknown> = {}) {
  return {
    attrs: { collapsed: true, ...attrs },
    type: { name: "spoiler" },
  } as any;
}

// Create full mock props required by nodeViewProps
function createMockProps(
  attrs: Record<string, unknown> = {},
  updateAttributes = vi.fn(),
) {
  return {
    node: createMockNode(attrs),
    updateAttributes,
    deleteNode: vi.fn(),
    selected: false,
    extension: {},
    getPos: () => 0,
    editor: {},
    decorations: [],
    view: {},
    HTMLAttributes: {},
  } as any;
}

describe("SpoilerView", () => {
  let updateAttributesMock: Mock;

  beforeEach(() => {
    updateAttributesMock = vi.fn();
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  describe("Basic Rendering", () => {
    it('shows "Показать содержимое" when collapsed', () => {
      const wrapper = mount(SpoilerView, {
        props: createMockProps({ collapsed: true }, updateAttributesMock),
      });
      expect(wrapper.find(".spoiler-head").text()).toBe("Показать содержимое");
    });

    it('shows "Скрыть содержимое" when expanded', () => {
      const wrapper = mount(SpoilerView, {
        props: createMockProps({ collapsed: false }, updateAttributesMock),
      });
      expect(wrapper.find(".spoiler-head").text()).toBe("Скрыть содержимое");
    });

    // [spoiler text="..."] names the button that shows the content.
    it("shows its caption when collapsed, and the default for an empty one", () => {
      const captioned = mount(SpoilerView, {
        props: createMockProps(
          { collapsed: true, text: "Карта" },
          updateAttributesMock,
        ),
      });
      expect(captioned.find(".spoiler-head").text()).toBe("Карта");

      const empty = mount(SpoilerView, {
        props: createMockProps(
          { collapsed: true, text: "" },
          updateAttributesMock,
        ),
      });
      expect(empty.find(".spoiler-head").text()).toBe("Показать содержимое");
    });

    // Open, a captioned spoiler keeps its caption on the button, as the post
    // draws it; only one without a caption says "Скрыть содержимое".
    it("keeps its caption when expanded", () => {
      const captioned = mount(SpoilerView, {
        props: createMockProps(
          { collapsed: false, text: "Карта" },
          updateAttributesMock,
        ),
      });
      expect(captioned.find(".spoiler-head").text()).toBe("Карта");
    });
  });

  describe("Collapse/Expand", () => {
    it("toggles collapsed state on click", async () => {
      const wrapper = mount(SpoilerView, {
        props: createMockProps({ collapsed: true }, updateAttributesMock),
      });

      await wrapper.find(".spoiler-head").trigger("click");
      expect(updateAttributesMock).toHaveBeenCalledWith({ collapsed: false });
    });

    it("shows content when not collapsed", () => {
      const wrapper = mount(SpoilerView, {
        props: createMockProps({ collapsed: false }, updateAttributesMock),
      });
      // Expanded state = the collapse wrapper carries the `open` class
      // (grid-template-rows 1fr reveals the content)
      expect(wrapper.find(".bb-collapse").classes()).toContain("open");
    });

    it("hides content when collapsed", () => {
      const wrapper = mount(SpoilerView, {
        props: createMockProps({ collapsed: true }, updateAttributesMock),
      });
      // Collapsed state = no `open` class (grid-template-rows 0fr clips
      // the content to zero height)
      expect(wrapper.find(".bb-collapse").classes()).not.toContain("open");
    });
  });

  describe("Keyboard Accessibility", () => {
    it("toggles on Enter key", async () => {
      const wrapper = mount(SpoilerView, {
        props: createMockProps({ collapsed: true }, updateAttributesMock),
      });

      await wrapper.find(".spoiler-head").trigger("keydown.enter");
      expect(updateAttributesMock).toHaveBeenCalledWith({ collapsed: false });
    });

    it("toggles on Space key", async () => {
      const wrapper = mount(SpoilerView, {
        props: createMockProps({ collapsed: true }, updateAttributesMock),
      });

      await wrapper.find(".spoiler-head").trigger("keydown.space");
      expect(updateAttributesMock).toHaveBeenCalledWith({ collapsed: false });
    });
  });

  describe("ARIA Attributes", () => {
    it('toggle has aria-expanded="false" when collapsed', () => {
      const wrapper = mount(SpoilerView, {
        props: createMockProps({ collapsed: true }, updateAttributesMock),
      });
      expect(wrapper.find(".spoiler-head").attributes("aria-expanded")).toBe(
        "false",
      );
    });

    it('toggle has aria-expanded="true" when expanded', () => {
      const wrapper = mount(SpoilerView, {
        props: createMockProps({ collapsed: false }, updateAttributesMock),
      });
      expect(wrapper.find(".spoiler-head").attributes("aria-expanded")).toBe(
        "true",
      );
    });

    it('content has role="region"', () => {
      const wrapper = mount(SpoilerView, {
        props: createMockProps({}, updateAttributesMock),
      });
      expect(wrapper.find(".spoiler").attributes("role")).toBe("region");
    });
  });
});

describe("NsfwView", () => {
  let updateAttributesMock: Mock;

  beforeEach(() => {
    updateAttributesMock = vi.fn();
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  describe("Basic Rendering", () => {
    it('shows "Показать шокирующий контент" when collapsed', () => {
      const wrapper = mount(NsfwView, {
        props: createMockProps({ collapsed: true }, updateAttributesMock),
      });
      expect(wrapper.find(".nsfw-head").text()).toBe(
        "Показать шокирующий контент",
      );
    });

    it('shows "Скрыть шокирующий контент" when expanded', () => {
      const wrapper = mount(NsfwView, {
        props: createMockProps({ collapsed: false }, updateAttributesMock),
      });
      expect(wrapper.find(".nsfw-head").text()).toBe(
        "Скрыть шокирующий контент",
      );
    });
  });

  describe("Collapse/Expand", () => {
    it("toggles collapsed state on click", async () => {
      const wrapper = mount(NsfwView, {
        props: createMockProps({ collapsed: true }, updateAttributesMock),
      });

      await wrapper.find(".nsfw-head").trigger("click");
      expect(updateAttributesMock).toHaveBeenCalledWith({ collapsed: false });
    });

    it("shows content when not collapsed", () => {
      const wrapper = mount(NsfwView, {
        props: createMockProps({ collapsed: false }, updateAttributesMock),
      });
      // Expanded state = the collapse wrapper carries the `open` class
      expect(wrapper.find(".bb-collapse").classes()).toContain("open");
    });

    it("hides content when collapsed", () => {
      const wrapper = mount(NsfwView, {
        props: createMockProps({ collapsed: true }, updateAttributesMock),
      });
      // Collapsed state = no `open` class (grid-template-rows 0fr clips
      // the content to zero height)
      expect(wrapper.find(".bb-collapse").classes()).not.toContain("open");
    });
  });

  describe("Keyboard Accessibility", () => {
    it("toggles on Enter key", async () => {
      const wrapper = mount(NsfwView, {
        props: createMockProps({ collapsed: true }, updateAttributesMock),
      });

      await wrapper.find(".nsfw-head").trigger("keydown.enter");
      expect(updateAttributesMock).toHaveBeenCalledWith({ collapsed: false });
    });

    it("toggles on Space key", async () => {
      const wrapper = mount(NsfwView, {
        props: createMockProps({ collapsed: true }, updateAttributesMock),
      });

      await wrapper.find(".nsfw-head").trigger("keydown.space");
      expect(updateAttributesMock).toHaveBeenCalledWith({ collapsed: false });
    });
  });

  describe("18+ Overlay", () => {
    it("shows overlay when expanded but not confirmed", () => {
      const wrapper = mount(NsfwView, {
        props: createMockProps({ collapsed: false }, updateAttributesMock),
      });
      expect(wrapper.find(".nsfw-overlay").exists()).toBe(true);
    });

    it("fades out overlay after clicking it", async () => {
      const wrapper = mount(NsfwView, {
        props: createMockProps({ collapsed: false }, updateAttributesMock),
      });

      await wrapper.find(".nsfw-overlay").trigger("click");
      // The overlay stays mounted and fades out via the CSS transition —
      // the `confirmed` class drives opacity/visibility
      expect(wrapper.find(".nsfw-overlay").classes()).toContain("confirmed");
    });

    it("overlay has warning text", () => {
      const wrapper = mount(NsfwView, {
        props: createMockProps({ collapsed: false }, updateAttributesMock),
      });
      expect(wrapper.find(".nsfw-warning").text()).toContain("18 лет");
    });
  });
});

describe.each([
  { view: SpoilerView, root: ".spoiler-wrapper", head: ".spoiler-head" },
  { view: NsfwView, root: ".nsfw-wrapper", head: ".nsfw-head" },
])("the head of $root", ({ view, root, head }) => {
  it("is a focusable span button the editor does not edit", () => {
    const wrapper = mount(view, { props: createMockProps() });
    const toggle = wrapper.find(`${root} ${head}`);
    expect(toggle.element.tagName).toBe("SPAN");
    expect(toggle.attributes("role")).toBe("button");
    expect(toggle.attributes("tabindex")).toBe("0");
    expect(toggle.attributes("contenteditable")).toBe("false");
  });
});
