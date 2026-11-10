/**
 * @vitest-environment jsdom
 */

// The rank ladder is walked whole: the bug it guards against is an off-by-one rank.
import { describe, it, expect, beforeEach, vi } from "vitest";
import { mount, flushPromises } from "@vue/test-utils";
import { setActivePinia, createPinia } from "pinia";
import { useAuthStore } from "@/entities/user";
import { UserRole, type User } from "@/shared/api/models/common";
import { createMockUser } from "@/entities/forum/model/store.fixtures";
import type { WebsiteTestimonial } from "@/shared/api/models/community";
import TestimonialEditable from "./TestimonialEditable.vue";

const { mockUpdateTestimonial } = vi.hoisted(() => ({
  mockUpdateTestimonial: vi.fn(),
}));

vi.mock("@/entities/testimonial/api", () => ({
  testimonialApi: {
    getTestimonials: vi.fn(),
    createTestimonial: vi.fn(),
    updateTestimonial: mockUpdateTestimonial,
    deleteTestimonial: vi.fn(),
  },
}));

const AUTHOR_ID = "u-author";
const STRANGER_ID = "u-stranger";

const testimonial = {
  id: "t-1",
  author: { id: AUTHOR_ID, username: "Solohin" },
  text: "Лучший сайт для словесок",
  createdUtc: "2026-01-01T00:00:00Z",
} as unknown as WebsiteTestimonial;

const viewer = (id: string, role: UserRole): User => ({
  ...createMockUser(id, id),
  role,
});

// The card is stubbed down to its #actions slot, where the pencil stands.
const render = () =>
  mount(TestimonialEditable, {
    props: { testimonial },
    global: {
      stubs: {
        TestimonialCard: {
          template: '<div class="card-stub"><slot name="actions" /></div>',
        },
        TextArea: {
          props: ["modelValue"],
          template:
            '<textarea :value="modelValue" @input="$emit(\'update:modelValue\', $event.target.value)" />',
        },
      },
    },
  });

const PENCIL = 'button[aria-label="Редактировать"]';

const offered = () => render().find(PENCIL).exists();

describe("TestimonialEditable access", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
  });

  it("hides the control from a guest", () => {
    useAuthStore().user = null;
    expect(offered()).toBe(false);
  });

  it.each([UserRole.RegularUser, UserRole.Mentor, UserRole.Moderator])(
    "hides the control from a stranger with role %s",
    (role) => {
      useAuthStore().user = viewer(STRANGER_ID, role);
      expect(offered()).toBe(false);
    },
  );

  it("offers the control to the named author", () => {
    // The server names the author as its own branch of Edit.
    useAuthStore().user = viewer(AUTHOR_ID, UserRole.RegularUser);
    expect(offered()).toBe(true);
  });

  it.each([UserRole.SeniorModerator, UserRole.Admin])(
    "offers the control to %s who did not write it",
    (role) => {
      useAuthStore().user = viewer(STRANGER_ID, role);
      expect(offered()).toBe(true);
    },
  );
});

describe("TestimonialEditable editing", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
  });

  it("puts the current text in the editor and saves what came back", async () => {
    useAuthStore().user = viewer(STRANGER_ID, UserRole.Admin);
    mockUpdateTestimonial.mockResolvedValue({
      data: { resource: { ...testimonial, text: "Переписано" } },
      error: null,
    });

    const wrapper = render();
    await wrapper.find(PENCIL).trigger("click");

    const field = wrapper.find("textarea");
    expect(field.exists()).toBe(true);
    expect((field.element as HTMLTextAreaElement).value).toBe(
      "Лучший сайт для словесок",
    );

    await field.setValue("Переписано");
    await wrapper
      .findAll("button")
      .find((b) => b.text() === "Сохранить")!
      .trigger("click");
    await flushPromises();

    expect(mockUpdateTestimonial).toHaveBeenCalledWith("t-1", {
      text: "Переписано",
    });
    expect(wrapper.find("textarea").exists()).toBe(false);
  });

  it("asks the server for nothing when the edit is cancelled", async () => {
    useAuthStore().user = viewer(STRANGER_ID, UserRole.Admin);

    const wrapper = render();
    await wrapper.find(PENCIL).trigger("click");
    await wrapper.find("textarea").setValue("Передумал");
    await wrapper
      .findAll("button")
      .find((b) => b.text() === "Отмена")!
      .trigger("click");

    expect(mockUpdateTestimonial).not.toHaveBeenCalled();
    expect(wrapper.find("textarea").exists()).toBe(false);
  });
});
