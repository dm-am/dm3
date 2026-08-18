/**
 * @vitest-environment jsdom
 */

/**
 * The screen that writes a recommendation — the one thing the site could not
 * do: both listings of recommendations existed with nothing able to add a row.
 *
 * Two rules are under test. The field is drawn only where the server said the
 * POST would be accepted, and a refusal is shown in the server's own sentence
 * instead of a paraphrase — the URL is reachable directly, so the page cannot
 * lean on the profile having hidden the link. And once the recommendation
 * lands, the reader is taken to where it now lives: the recipient's received
 * page. A refused POST costs nothing that was typed.
 */
import { describe, expect, it, vi, beforeEach } from "vitest";
import { defineComponent, h } from "vue";
import { flushPromises, mount } from "@vue/test-utils";
import { userApi } from "@/entities/user";
import ProfileEndorsementForm from "./ProfileEndorsementForm.vue";

const { push } = vi.hoisted(() => ({ push: vi.fn() }));

vi.mock("vue-router", () => ({
  useRoute: () => ({ params: { username: "Reader" }, query: {} }),
  useRouter: () => ({ push }),
}));

vi.mock("@/entities/user", () => ({
  userApi: {
    getUser: vi.fn(),
    getEndorsementEligibility: vi.fn(),
    createUserEndorsement: vi.fn(),
  },
}));

/** The eligibility read answers an Envelope, like every single-resource read. */
const eligibility = (resource: unknown) =>
  vi
    .mocked(userApi.getEndorsementEligibility)
    .mockResolvedValue({ data: { resource }, error: null } as never);

/** form-field that renders its errors, so a refusal under the field is visible. */
const FormFieldStub = defineComponent({
  props: { errors: { type: Array, default: () => [] } },
  setup:
    (props, { slots }) =>
    () =>
      h("div", [
        slots.default?.(),
        ...(props.errors as string[]).map((e) =>
          h("span", { class: "field-error" }, e),
        ),
      ]),
});

const TextAreaStub = defineComponent({
  props: { modelValue: { type: String, default: "" } },
  emits: ["update:modelValue"],
  setup:
    (props, { emit }) =>
    () =>
      h("textarea", {
        class: "endorsement-text",
        value: props.modelValue,
        onInput: (e: Event) =>
          emit("update:modelValue", (e.target as HTMLTextAreaElement).value),
      }),
});

const ButtonStub = defineComponent({
  props: { disabled: Boolean },
  setup:
    (props, { slots, emit }) =>
    () =>
      h(
        "button",
        {
          class: "submit",
          disabled: props.disabled,
          onClick: () => emit("click"),
        },
        slots.default?.(),
      ),
});

async function render() {
  const wrapper = mount(ProfileEndorsementForm, {
    global: {
      stubs: {
        "form-field": FormFieldStub,
        TextArea: TextAreaStub,
        Button: ButtonStub,
        "secondary-text": { template: "<p class='refusal'><slot /></p>" },
        ProfileSubpageHeader: { template: "<header><slot /></header>" },
        ErrorPage: { template: "<div class='error-page' />" },
        ErrorState: {
          props: ["message"],
          template: "<div class='error-state'>{{ message }}</div>",
        },
        RouterLink: { template: "<a><slot /></a>" },
      },
    },
  });
  await flushPromises();
  return wrapper;
}

async function type(wrapper: Awaited<ReturnType<typeof render>>, text: string) {
  await wrapper.find("textarea.endorsement-text").setValue(text);
}

describe("ProfileEndorsementForm access", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(userApi.getUser).mockResolvedValue({
      data: { username: "Reader" },
      error: null,
    } as never);
  });

  it("draws the field where the server says the POST would be accepted", async () => {
    eligibility({ canCreate: true });

    const wrapper = await render();

    expect(wrapper.find("textarea.endorsement-text").exists()).toBe(true);
    expect(wrapper.find("p.refusal").exists()).toBe(false);
  });

  // The two refusals the flow exists to respect, printed as the server wrote
  // them: a paraphrase here would be a second copy of the rule.
  it.each([
    ["Нельзя рекомендовать самого себя"],
    ["Вы уже рекомендовали этого пользователя"],
  ])("shows %s instead of the field", async (reason) => {
    eligibility({ canCreate: false, reason });

    const wrapper = await render();

    expect(wrapper.find("textarea.endorsement-text").exists()).toBe(false);
    expect(wrapper.find("p.refusal").text()).toBe(reason);
  });

  it("draws no field when the question itself failed", async () => {
    vi.mocked(userApi.getEndorsementEligibility).mockResolvedValue({
      data: null,
      error: { status: 500, title: "" },
    } as never);

    const wrapper = await render();

    expect(wrapper.find("textarea.endorsement-text").exists()).toBe(false);
    expect(wrapper.find(".error-state").exists()).toBe(true);
  });
});

describe("ProfileEndorsementForm submit", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(userApi.getUser).mockResolvedValue({
      data: { username: "Reader" },
      error: null,
    } as never);
    eligibility({ canCreate: true });
  });

  it("sends the trimmed text to the recipient named by the URL", async () => {
    vi.mocked(userApi.createUserEndorsement).mockResolvedValue({
      data: { id: "e-1" },
      error: null,
    } as never);
    const wrapper = await render();

    await type(wrapper, "  Держит темп и не бросает сцену  ");
    await wrapper.find("button.submit").trigger("click");
    await flushPromises();

    expect(vi.mocked(userApi.createUserEndorsement)).toHaveBeenCalledWith(
      "Reader",
      { text: "Держит темп и не бросает сцену" },
    );
  });

  it("takes the reader to where the recommendation now lives", async () => {
    vi.mocked(userApi.createUserEndorsement).mockResolvedValue({
      data: { id: "e-1" },
      error: null,
    } as never);
    const wrapper = await render();

    await type(wrapper, "Держит темп и не бросает сцену");
    await wrapper.find("button.submit").trigger("click");
    await flushPromises();

    expect(push).toHaveBeenCalledWith({
      name: "received-endorsements",
      params: { username: "Reader" },
    });
  });

  it("keeps the text and prints the refusal when the pair already has one", async () => {
    vi.mocked(userApi.createUserEndorsement).mockResolvedValue({
      data: null,
      error: { status: 409, title: "Вы уже рекомендовали этого пользователя" },
    } as never);
    const wrapper = await render();

    await type(wrapper, "Держит темп и не бросает сцену");
    await wrapper.find("button.submit").trigger("click");
    await flushPromises();

    expect(push).not.toHaveBeenCalled();
    expect(wrapper.find("span.field-error").text()).toBe(
      "Вы уже рекомендовали этого пользователя",
    );
    expect(
      (wrapper.find("textarea.endorsement-text").element as HTMLTextAreaElement)
        .value,
    ).toBe("Держит темп и не бросает сцену");
  });

  it("refuses to send a text shorter than the server's minimum", async () => {
    const wrapper = await render();

    await type(wrapper, "Хорош");
    await wrapper.find("button.submit").trigger("click");
    await flushPromises();

    expect(vi.mocked(userApi.createUserEndorsement)).not.toHaveBeenCalled();
  });
});
