/**
 * @vitest-environment jsdom
 */

/**
 * The form's only job before a word is typed is deciding who is shown it, and
 * that decision belongs to the server: WebsiteTestimonialIntentionResolver
 * admits Create for SeniorModerator and above. An ordinary moderator used to be
 * offered the form here and answered 403 on submit — an offer the site had no
 * right to make.
 *
 * The rank ladder is walked whole rather than sampled, because the bug the
 * ladder guards against is exactly an off-by-one rank.
 */
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

/** Is the "+ Добавить отзыв" affordance on the page at all? */
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

/**
 * A testimonial about the site is posted ON BEHALF OF a participant: senior
 * moderation submits it, the named person signs it. The form carried no author
 * at all, so every entry added here was signed by the moderator who typed it —
 * a claim about the wrong person, on a page the whole site reads.
 */
describe("CreateTestimonialForm author", () => {
  /** The picker and the text field, driven through their v-model. */
  const renderOpen = () =>
    mount(CreateTestimonialForm, {
      global: {
        stubs: {
          // The real field renders `errors` itself; the stub has to, or the
          // refusal a test asserts on is on a prop nobody drew.
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

    // Not the viewer ("Reviewer") who submitted it.
    expect(mockCreateTestimonial).toHaveBeenCalledWith({
      authorUsername: "Solohin",
      text: "Лучший сайт для словесок",
    });
  });

  it("names the author field when the server does not know the participant", async () => {
    useAuthStore().user = viewer(UserRole.Admin);
    // The picker can be typed past, so the existence check is the server's and
    // its refusal has to land on the field that caused it.
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
