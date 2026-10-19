/**
 * @vitest-environment jsdom
 */

// The rank ladder is walked whole: the bug it guards against is an off-by-one rank.
import { describe, it, expect, beforeEach, vi } from "vitest";
import { mount, flushPromises } from "@vue/test-utils";
import { setActivePinia, createPinia } from "pinia";
import { useAuthStore } from "@/entities/user";
import { UserRole, type User } from "@/shared/api/models/common";
import CreateTestimonialForm from "./CreateTestimonialForm.vue";

const { mockCreateTestimonial } = vi.hoisted(() => ({
  mockCreateTestimonial: vi.fn(),
}));

vi.mock("@/entities/testimonial/api", () => ({
  testimonialApi: {
    getTestimonials: vi.fn(),
    createTestimonial: mockCreateTestimonial,
    updateTestimonial: vi.fn(),
    deleteTestimonial: vi.fn(),
  },
}));

const viewer = (role: UserRole) =>
  ({ id: "u-1", username: "Reviewer", role }) as unknown as User;

const render = () =>
  mount(CreateTestimonialForm, {
    global: {
      stubs: {
        "form-field": true,
        UserAutocomplete: true,
        TextArea: true,
        Button: true,
      },
    },
  });

const offered = () => render().find("button.toggle-link").exists();

describe("CreateTestimonialForm access", () => {
  beforeEach(() => setActivePinia(createPinia()));

  it("hides the form from a guest", () => {
    useAuthStore().user = null;
    expect(offered()).toBe(false);
  });

  it.each([UserRole.RegularUser, UserRole.Mentor, UserRole.Moderator])(
    "hides the form from %s — the server refuses Create below senior",
    (role) => {
      useAuthStore().user = viewer(role);
      expect(offered()).toBe(false);
    },
  );

  it.each([UserRole.SeniorModerator, UserRole.Admin])(
    "offers the form to %s — the rank the server admits",
    (role) => {
      useAuthStore().user = viewer(role);
      expect(offered()).toBe(true);
    },
  );
});

describe("CreateTestimonialForm author", () => {
  const renderOpen = () =>
    mount(CreateTestimonialForm, {
      global: {
        stubs: {
          // The stub renders `errors`, or the refusal asserted on is drawn by nobody.
          "form-field": {
            props: ["errors"],
            template:
              '<div><slot /><span v-for="e in errors" :key="e" class="field-error">{{ e }}</span></div>',
          },
          UserAutocomplete: {
            props: ["modelValue"],
            emits: ["update:modelValue"],
            template:
              '<input class="author-picker" :value="modelValue" @input="$emit(\'update:modelValue\', $event.target.value)" />',
          },
          TextArea: {
            props: ["modelValue"],
            emits: ["update:modelValue"],
            template:
              '<textarea class="testimonial-text" :value="modelValue" @input="$emit(\'update:modelValue\', $event.target.value)" />',
          },
          Button: {
            template:
              '<button class="submit" @click="$emit(\'click\')"><slot /></button>',
          },
        },
      },
    });

  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
  });

  it("asks for the author with the same picker the other moderation forms use", () => {
    useAuthStore().user = viewer(UserRole.Admin);
    expect(renderOpen().find(".author-picker").exists()).toBe(true);
  });

  it("posts the testimonial in the name of the chosen participant", async () => {
    useAuthStore().user = viewer(UserRole.Admin);
    mockCreateTestimonial.mockResolvedValue({
      data: { resource: { id: "t-9" } },
      error: null,
    });

    const wrapper = renderOpen();
    await wrapper.find(".author-picker").setValue("Solohin");
    await wrapper
      .find(".testimonial-text")
      .setValue("Лучший сайт для словесок");
    await wrapper.find("button.submit").trigger("click");
    await flushPromises();

    expect(mockCreateTestimonial).toHaveBeenCalledWith({
      authorUsername: "Solohin",
      text: "Лучший сайт для словесок",
    });
  });

  it("names the author field when the server does not know the participant", async () => {
    useAuthStore().user = viewer(UserRole.Admin);
    mockCreateTestimonial.mockResolvedValue({
      data: null,
      error: { status: 404, title: "Пользователь Nobody не найден" },
    });

    const wrapper = renderOpen();
    await wrapper.find(".author-picker").setValue("Nobody");
    await wrapper
      .find(".testimonial-text")
      .setValue("Лучший сайт для словесок");
    await wrapper.find("button.submit").trigger("click");
    await flushPromises();

    expect(wrapper.text()).toContain("Пользователь Nobody не найден");
  });

  it("refuses to submit before an author is named", async () => {
    useAuthStore().user = viewer(UserRole.Admin);

    const wrapper = renderOpen();
    await wrapper
      .find(".testimonial-text")
      .setValue("Лучший сайт для словесок");
    await wrapper.find("button.submit").trigger("click");
    await flushPromises();

    expect(mockCreateTestimonial).not.toHaveBeenCalled();
    expect(wrapper.text()).toContain("Выберите автора отзыва");
  });
});
