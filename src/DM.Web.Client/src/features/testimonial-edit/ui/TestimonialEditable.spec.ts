/**
 * @vitest-environment jsdom
 */

/**
 * Who is offered "Редактировать" is the server's decision, and this file is
 * where the client's copy of it is held to it. WebsiteTestimonialIntentionResolver
 * admits Edit to the named author or to SeniorModerator and above; a moderator
 * one rank below is refused, and an offer the site has no right to make ends in
 * a 403 after the reader has already retyped their text.
 *
 * The ladder is walked whole rather than sampled, because what it guards
 * against is exactly an off-by-one rank.
 */
import { describe, it, expect, beforeEach, vi } from "vitest";
import { mount, flushPromises } from "@vue/test-utils";
import { setActivePinia, createPinia } from "pinia";
import { useAuthStore } from "@/entities/user";
import { UserRole, type User } from "@/shared/api/models/common";
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

const viewer = (id: string, role: UserRole) =>
  ({ id, username: id, role }) as unknown as User;

/**
 * The card is stubbed down to its #controls slot: this file is about the
 * control, and the real card drags in the bubble, the avatar and the router.
 */
const render = () =>
  mount(TestimonialEditable, {
    props: { testimonial },
    global: {
      stubs: {
        TestimonialCard: {
          template: '<div class="card-stub"><slot name="controls" /></div>',
        },
        "secondary-text": { template: "<span><slot /></span>" },
        TextArea: {
          props: ["modelValue"],
          template:
            '<textarea :value="modelValue" @input="$emit(\'update:modelValue\', $event.target.value)" />',
        },
      },
    },
  });

const offered = () => render().find("button.testimonial-edit-btn").exists();

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
    // The server names the author as its own branch of Edit, so an ordinary
    // participant whose name is on the entry may rewrite it.
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
    await wrapper.find("button.testimonial-edit-btn").trigger("click");

    const field = wrapper.find("textarea");
    expect(field.exists()).toBe(true);
    expect((field.element as HTMLTextAreaElement).value).toBe(
      "Лучший сайт для словесок",
    );

    await field.setValue("Переписано");
    await wrapper.find("button.save-btn").trigger("click");
    await flushPromises();

    expect(mockUpdateTestimonial).toHaveBeenCalledWith("t-1", {
      text: "Переписано",
    });
    // Back to the card once the server took it.
    expect(wrapper.find("textarea").exists()).toBe(false);
  });

  it("asks the server for nothing when the edit is cancelled", async () => {
    useAuthStore().user = viewer(STRANGER_ID, UserRole.Admin);

    const wrapper = render();
    await wrapper.find("button.testimonial-edit-btn").trigger("click");
    await wrapper.find("textarea").setValue("Передумал");
    await wrapper.findAll("button.action-btn").at(-1)!.trigger("click");

    expect(mockUpdateTestimonial).not.toHaveBeenCalled();
    expect(wrapper.find("textarea").exists()).toBe(false);
  });
});
