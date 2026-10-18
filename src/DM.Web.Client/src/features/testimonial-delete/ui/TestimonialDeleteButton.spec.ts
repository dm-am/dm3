/**
 * @vitest-environment jsdom
 */

/**
 * The trash in the corner of a testimonial is an admin's, and nobody else is
 * shown it. It asks before it deletes: the question waits shut for the server,
 * prints a refusal in itself, so the request tells the interceptor to stay
 * quiet, and closes once the entry is gone.
 */
import { describe, it, expect, afterEach, beforeEach, vi } from "vitest";
import { enableAutoUnmount, flushPromises, mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { useAuthStore } from "@/entities/user";
import { UserRole, type User } from "@/shared/api/models/common";
import type { WebsiteTestimonial } from "@/shared/api/models/community";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import TestimonialDeleteButton from "./TestimonialDeleteButton.vue";

const { mockDeleteTestimonial } = vi.hoisted(() => ({
  mockDeleteTestimonial: vi.fn(),
}));

vi.mock("@/entities/testimonial/api", () => ({
  testimonialApi: {
    getTestimonials: vi.fn(),
    createTestimonial: vi.fn(),
    updateTestimonial: vi.fn(),
    deleteTestimonial: mockDeleteTestimonial,
  },
}));

const testimonial = {
  id: "t-1",
  author: { id: "u-author", username: "Solohin" },
  text: "Лучший сайт для словесок",
  createdUtc: "2026-01-01T00:00:00Z",
} as unknown as WebsiteTestimonial;

const viewer = (role: UserRole) =>
  ({ id: "u-viewer", username: "viewer", role }) as unknown as User;

const TRASH = 'button[aria-label="Удалить"]';

const render = () => mount(TestimonialDeleteButton, { props: { testimonial } });

// A question left open by one test would answer the next one's clicks.
enableAutoUnmount(afterEach);

beforeEach(() => {
  setActivePinia(createPinia());
  vi.clearAllMocks();
});

describe("TestimonialDeleteButton", () => {
  it("shows a guest nothing", () => {
    useAuthStore().user = null;
    expect(render().find(TRASH).exists()).toBe(false);
  });

  it.each([UserRole.RegularUser, UserRole.Moderator])(
    "shows %s nothing",
    (role) => {
      useAuthStore().user = viewer(role);
      expect(render().find(TRASH).exists()).toBe(false);
    },
  );

  it("asks an admin before it deletes, then deletes with the refusal its own", async () => {
    useAuthStore().user = viewer(UserRole.Admin);
    mockDeleteTestimonial.mockResolvedValue({ data: undefined, error: null });
    const wrapper = render();

    await wrapper.get(TRASH).trigger("click");
    expect(mockDeleteTestimonial).not.toHaveBeenCalled();
    const dialog = wrapper.findComponent(ConfirmDialog);
    expect(dialog.props("show")).toBe(true);

    document.body
      .querySelector<HTMLElement>(".dialog-container .dialog-btn-submit")!
      .click();
    await flushPromises();

    expect(mockDeleteTestimonial).toHaveBeenCalledWith("t-1", {
      ownsRefusal: true,
      ownsRateLimit: true,
    });
    expect(dialog.props("show")).toBe(false);
  });
});
