/**
 * @vitest-environment jsdom
 */

/**
 * InputDialog Component Tests
 *
 * Tests for the modal input dialog component.
 * Covers rendering, validation, XSS prevention, keyboard handling,
 * and accessibility.
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount, config } from "@vue/test-utils";
import { nextTick } from "vue";
import InputDialog from "./InputDialog.vue";
import type { InputField } from "./InputDialog.vue";

// Stub Teleport to render content in place (not to body)
config.global.stubs = {
  Teleport: true,
};

describe("InputDialog", () => {
  const defaultFields: InputField[] = [
    {
      name: "url",
      label: "URL",
      type: "url",
      placeholder: "https://example.com",
      required: true,
    },
  ];

  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  // ============================================================================
  // BASIC RENDERING
  // ============================================================================

  describe("Basic Rendering", () => {
    it("renders when show is true", () => {
      const wrapper = mount(InputDialog, {
        props: {
          show: true,
          title: "Test Dialog",
          fields: defaultFields,
        },
      });

      expect(wrapper.find(".dialog-backdrop").exists()).toBe(true);
    });

    it("does not render when show is false", () => {
      const wrapper = mount(InputDialog, {
        props: {
          show: false,
          title: "Test Dialog",
          fields: defaultFields,
        },
      });

      expect(wrapper.find(".dialog-backdrop").exists()).toBe(false);
    });

    it("renders title", () => {
      const wrapper = mount(InputDialog, {
        props: {
          show: true,
          title: "My Custom Title",
          fields: defaultFields,
        },
      });

      expect(wrapper.find(".dialog-title").text()).toBe("My Custom Title");
    });

    it("renders input fields", () => {
      const fields: InputField[] = [
        { name: "field1", label: "Field 1", type: "text" },
        { name: "field2", label: "Field 2", type: "text" },
      ];

      const wrapper = mount(InputDialog, {
        props: {
          show: true,
          title: "Test",
          fields,
        },
      });

      expect(wrapper.findAll(".dialog-field").length).toBe(2);
    });

    it("renders submit and cancel buttons", () => {
      const wrapper = mount(InputDialog, {
        props: {
          show: true,
          title: "Test",
          fields: defaultFields,
        },
      });

      expect(wrapper.find(".dialog-btn-submit").exists()).toBe(true);
      expect(wrapper.find(".dialog-btn-cancel").exists()).toBe(true);
    });

    it("uses custom button labels", () => {
      const wrapper = mount(InputDialog, {
        props: {
          show: true,
          title: "Test",
          fields: defaultFields,
          submitLabel: "Save",
          cancelLabel: "Close",
        },
      });

      expect(wrapper.find(".dialog-btn-submit").text()).toBe("Save");
      expect(wrapper.find(".dialog-btn-cancel").text()).toBe("Close");
    });
  });

  // ============================================================================
  // ACCESSIBILITY
  // ============================================================================

  describe("Accessibility", () => {
    it('dialog has role="dialog"', () => {
      const wrapper = mount(InputDialog, {
        props: {
          show: true,
          title: "Test",
          fields: defaultFields,
        },
      });

      expect(wrapper.find(".dialog-container").attributes("role")).toBe(
        "dialog",
      );
    });

    it('dialog has aria-modal="true"', () => {
      const wrapper = mount(InputDialog, {
        props: {
          show: true,
          title: "Test",
          fields: defaultFields,
        },
      });

      expect(wrapper.find(".dialog-container").attributes("aria-modal")).toBe(
        "true",
      );
    });

    it("dialog has aria-label", () => {
      const wrapper = mount(InputDialog, {
        props: {
          show: true,
          title: "My Title",
          fields: defaultFields,
        },
      });

      expect(wrapper.find(".dialog-container").attributes("aria-label")).toBe(
        "My Title",
      );
    });

    it("close button has aria-label", () => {
      const wrapper = mount(InputDialog, {
        props: {
          show: true,
          title: "Test",
          fields: defaultFields,
        },
      });

      expect(wrapper.find(".dialog-close").attributes("aria-label")).toBe(
        "Закрыть",
      );
    });

    it("required fields show asterisk", () => {
      const fields: InputField[] = [
        { name: "req", label: "Required Field", type: "text", required: true },
      ];

      const wrapper = mount(InputDialog, {
        props: {
          show: true,
          title: "Test",
          fields,
        },
      });

      expect(wrapper.find(".required").exists()).toBe(true);
      expect(wrapper.find(".required").text()).toBe("*");
    });
  });

  // ============================================================================
  // VALIDATION
  // ============================================================================

  describe("Validation", () => {
    it("shows error for required empty field", async () => {
      const fields: InputField[] = [
        { name: "field", label: "Field", type: "text", required: true },
      ];

      const wrapper = mount(InputDialog, {
        props: {
          show: true,
          title: "Test",
          fields,
        },
      });

      await wrapper.find(".dialog-btn-submit").trigger("click");
      await nextTick();

      expect(wrapper.find(".dialog-error").exists()).toBe(true);
      expect(wrapper.find(".dialog-error").text()).toBe("Это поле обязательно");
    });

    it("validates URL format", async () => {
      const wrapper = mount(InputDialog, {
        props: {
          show: true,
          title: "Test",
          fields: defaultFields,
        },
      });

      const input = wrapper.find(".dialog-input");
      await input.setValue("not-a-url");
      await input.trigger("blur");
      await nextTick();

      expect(wrapper.find(".dialog-error").exists()).toBe(true);
      expect(wrapper.find(".dialog-error").text()).toBe(
        "Введите корректный URL",
      );
    });

    it("accepts valid HTTP URL", async () => {
      const wrapper = mount(InputDialog, {
        props: {
          show: true,
          title: "Test",
          fields: defaultFields,
        },
      });

      const input = wrapper.find(".dialog-input");
      await input.setValue("https://example.com");
      await input.trigger("blur");
      await nextTick();

      expect(wrapper.find(".dialog-error").exists()).toBe(false);
    });

    it("adds error class to invalid input", async () => {
      const wrapper = mount(InputDialog, {
        props: {
          show: true,
          title: "Test",
          fields: defaultFields,
        },
      });

      const input = wrapper.find(".dialog-input");
      await input.setValue("invalid");
      await input.trigger("blur");
      await nextTick();

      expect(input.classes()).toContain("error");
    });

    it("uses custom validator", async () => {
      const fields: InputField[] = [
        {
          name: "custom",
          label: "Custom",
          type: "text",
          validator: (value: string) => {
            if (value.length < 5) return "Must be at least 5 characters";
            return null;
          },
        },
      ];

      const wrapper = mount(InputDialog, {
        props: {
          show: true,
          title: "Test",
          fields,
        },
      });

      const input = wrapper.find(".dialog-input");
      await input.setValue("abc");
      await input.trigger("blur");
      await nextTick();

      expect(wrapper.find(".dialog-error").text()).toBe(
        "Must be at least 5 characters",
      );
    });
  });

  // ============================================================================
  // XSS PREVENTION
  // ============================================================================

  describe("XSS Prevention", () => {
    it("rejects javascript: URLs", async () => {
      const wrapper = mount(InputDialog, {
        props: {
          show: true,
          title: "Test",
          fields: defaultFields,
        },
      });

      const input = wrapper.find(".dialog-input");
      await input.setValue("javascript:alert(1)");
      await input.trigger("blur");
      await nextTick();

      expect(wrapper.find(".dialog-error").exists()).toBe(true);
      expect(wrapper.find(".dialog-error").text()).toBe(
        "Разрешены только HTTP и HTTPS ссылки",
      );
    });

    it("rejects data: URLs", async () => {
      const wrapper = mount(InputDialog, {
        props: {
          show: true,
          title: "Test",
          fields: defaultFields,
        },
      });

      const input = wrapper.find(".dialog-input");
      await input.setValue("data:text/html,<script>alert(1)</script>");
      await input.trigger("blur");
      await nextTick();

      expect(wrapper.find(".dialog-error").exists()).toBe(true);
    });

    it("accepts mailto: URLs", async () => {
      const wrapper = mount(InputDialog, {
        props: {
          show: true,
          title: "Test",
          fields: defaultFields,
        },
      });

      const input = wrapper.find(".dialog-input");
      await input.setValue("mailto:test@example.com");
      await input.trigger("blur");
      await nextTick();

      expect(wrapper.find(".dialog-error").exists()).toBe(false);
    });

    it("rejects ftp: URLs", async () => {
      const wrapper = mount(InputDialog, {
        props: {
          show: true,
          title: "Test",
          fields: defaultFields,
        },
      });

      const input = wrapper.find(".dialog-input");
      await input.setValue("ftp://files.example.com/file.txt");
      await input.trigger("blur");
      await nextTick();

      expect(wrapper.find(".dialog-error").exists()).toBe(true);
    });
  });

  // ============================================================================
  // KEYBOARD HANDLING
  // ============================================================================

  describe("Keyboard Handling", () => {
    it("closes on Escape key", async () => {
      const wrapper = mount(InputDialog, {
        props: {
          show: true,
          title: "Test",
          fields: defaultFields,
        },
      });

      await wrapper
        .find(".dialog-backdrop")
        .trigger("keydown", { key: "Escape" });
      await nextTick();

      expect(wrapper.emitted("update:show")).toBeTruthy();
      expect(wrapper.emitted("update:show")![0]).toEqual([false]);
    });

    it("submits on Enter key", async () => {
      const wrapper = mount(InputDialog, {
        props: {
          show: true,
          title: "Test",
          fields: defaultFields,
        },
      });

      const input = wrapper.find(".dialog-input");
      await input.setValue("https://example.com");
      await wrapper
        .find(".dialog-backdrop")
        .trigger("keydown", { key: "Enter" });
      await nextTick();

      expect(wrapper.emitted("submit")).toBeTruthy();
    });

    it("does not submit on Shift+Enter", async () => {
      const wrapper = mount(InputDialog, {
        props: {
          show: true,
          title: "Test",
          fields: defaultFields,
        },
      });

      const input = wrapper.find(".dialog-input");
      await input.setValue("https://example.com");
      await wrapper
        .find(".dialog-backdrop")
        .trigger("keydown", { key: "Enter", shiftKey: true });
      await nextTick();

      expect(wrapper.emitted("submit")).toBeFalsy();
    });
  });

  // ============================================================================
  // FORM SUBMISSION
  // ============================================================================

  describe("Form Submission", () => {
    it("emits submit with field values", async () => {
      const fields: InputField[] = [
        { name: "name", label: "Name", type: "text" },
        { name: "email", label: "Email", type: "email" },
      ];

      const wrapper = mount(InputDialog, {
        props: {
          show: true,
          title: "Test",
          fields,
        },
      });

      const inputs = wrapper.findAll(".dialog-input");
      await inputs[0].setValue("John");
      await inputs[1].setValue("john@example.com");
      await wrapper.find(".dialog-btn-submit").trigger("click");
      await nextTick();

      expect(wrapper.emitted("submit")).toBeTruthy();
      expect(wrapper.emitted("submit")![0]).toEqual([
        { name: "John", email: "john@example.com" },
      ]);
    });

    it("does not submit if validation fails", async () => {
      const fields: InputField[] = [
        { name: "field", label: "Field", type: "text", required: true },
      ];

      const wrapper = mount(InputDialog, {
        props: {
          show: true,
          title: "Test",
          fields,
        },
      });

      await wrapper.find(".dialog-btn-submit").trigger("click");
      await nextTick();

      expect(wrapper.emitted("submit")).toBeFalsy();
    });

    it("closes dialog after successful submit", async () => {
      const fields: InputField[] = [
        { name: "field", label: "Field", type: "text" },
      ];

      const wrapper = mount(InputDialog, {
        props: {
          show: true,
          title: "Test",
          fields,
        },
      });

      await wrapper.find(".dialog-input").setValue("value");
      await wrapper.find(".dialog-btn-submit").trigger("click");
      await nextTick();

      expect(wrapper.emitted("update:show")).toBeTruthy();
      expect(wrapper.emitted("update:show")![0]).toEqual([false]);
    });
  });

  // ============================================================================
  // CANCEL
  // ============================================================================

  describe("Cancel", () => {
    it("emits cancel when cancel button clicked", async () => {
      const wrapper = mount(InputDialog, {
        props: {
          show: true,
          title: "Test",
          fields: defaultFields,
        },
      });

      await wrapper.find(".dialog-btn-cancel").trigger("click");
      await nextTick();

      expect(wrapper.emitted("cancel")).toBeTruthy();
    });

    it("emits cancel when close button clicked", async () => {
      const wrapper = mount(InputDialog, {
        props: {
          show: true,
          title: "Test",
          fields: defaultFields,
        },
      });

      await wrapper.find(".dialog-close").trigger("click");
      await nextTick();

      expect(wrapper.emitted("cancel")).toBeTruthy();
    });

    it("emits cancel when backdrop clicked", async () => {
      const wrapper = mount(InputDialog, {
        props: {
          show: true,
          title: "Test",
          fields: defaultFields,
        },
      });

      // Click directly on backdrop (not on dialog-container)
      const backdrop = wrapper.find(".dialog-backdrop");
      await backdrop.trigger("click");
      await nextTick();

      expect(wrapper.emitted("cancel")).toBeTruthy();
    });

    it("closes dialog on cancel", async () => {
      const wrapper = mount(InputDialog, {
        props: {
          show: true,
          title: "Test",
          fields: defaultFields,
        },
      });

      await wrapper.find(".dialog-btn-cancel").trigger("click");
      await nextTick();

      expect(wrapper.emitted("update:show")).toBeTruthy();
      expect(wrapper.emitted("update:show")![0]).toEqual([false]);
    });
  });

  // ============================================================================
  // MULTIPLE FIELDS
  // ============================================================================

  describe("Multiple Fields", () => {
    it("renders all fields", () => {
      const fields: InputField[] = [
        { name: "url", label: "URL", type: "url" },
        { name: "text", label: "Text", type: "text" },
        { name: "email", label: "Email", type: "email" },
      ];

      const wrapper = mount(InputDialog, {
        props: {
          show: true,
          title: "Test",
          fields,
        },
      });

      expect(wrapper.findAll(".dialog-field").length).toBe(3);
    });

    it("applies correct input types", () => {
      const fields: InputField[] = [
        { name: "url", label: "URL", type: "url" },
        { name: "email", label: "Email", type: "email" },
        { name: "text", label: "Text", type: "text" },
      ];

      const wrapper = mount(InputDialog, {
        props: {
          show: true,
          title: "Test",
          fields,
        },
      });

      const inputs = wrapper.findAll(".dialog-input");
      expect(inputs[0].attributes("type")).toBe("url");
      expect(inputs[1].attributes("type")).toBe("email");
      expect(inputs[2].attributes("type")).toBe("text");
    });

    it("applies placeholders", () => {
      const fields: InputField[] = [
        { name: "url", label: "URL", placeholder: "Enter URL here" },
      ];

      const wrapper = mount(InputDialog, {
        props: {
          show: true,
          title: "Test",
          fields,
        },
      });

      expect(wrapper.find(".dialog-input").attributes("placeholder")).toBe(
        "Enter URL here",
      );
    });

    it("validates all fields on submit", async () => {
      const fields: InputField[] = [
        { name: "field1", label: "Field 1", required: true },
        { name: "field2", label: "Field 2", required: true },
      ];

      const wrapper = mount(InputDialog, {
        props: {
          show: true,
          title: "Test",
          fields,
        },
      });

      await wrapper.find(".dialog-btn-submit").trigger("click");
      await nextTick();

      expect(wrapper.findAll(".dialog-error").length).toBe(2);
    });
  });
});
