/**
 * @vitest-environment jsdom
 */

/**
 * The screen that writes a recommendation — the one thing the site could not
 * do: both listings of recommendations existed with nothing able to add a row.
 *
 * Two rules are under test. The field is drawn only where the server said the
 * POST would be accepted, and a "no" takes the reader to the profile instead
 * of a page with a sentence of refusal — the URL is reachable directly, so the
 * page cannot lean on the profile having hidden the link. And once the
 * recommendation lands, the reader is taken to where it now lives: the
 * recipient's received page. A refused POST costs nothing that was typed.
 */
import { describe, expect, it, vi, beforeEach } from "vitest";
import { defineComponent, h } from "vue";
import { flushPromises, mount } from "@vue/test-utils";
import { userApi } from "@/entities/user";
import ProfileEndorsementForm from "./ProfileEndorsementForm.vue";

const { push, replace } = vi.hoisted(() => ({
  push: vi.fn(),
  replace: vi.fn(),
}));

vi.mock("vue-router", () => ({
  useRoute: () => ({ params: { username: "Reader" }, query: {} }),
  useRouter: () => ({ push, replace }),
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
    expect(replace).not.toHaveBeenCalled();
  });

  // The profile draws the link here only on a "yes", so a "no" means the
  // address was typed: the reader is taken to the profile it names, with no
  // page standing under a sentence of refusal. Replaced, so that going back
  // does not land here again.
  it.each([
    ["Нельзя рекомендовать самого себя"],
    ["Вы уже рекомендовали этого пользователя"],
  ])("takes the reader to the profile on %s", async (reason) => {
    eligibility({ canCreate: false, reason });

    const wrapper = await render();

    expect(wrapper.find("textarea.endorsement-text").exists()).toBe(false);
    expect(wrapper.text()).not.toContain(reason);
    expect(replace).toHaveBeenCalledWith({
      name: "profile",
      params: { username: "Reader" },
    });
  });

  /**
   * One refusal, one carrier — and for the eligibility question the carrier is
   * this page: a failed check stands in the strip above the form, with the read
   * offered again. Both flags ride with it, the wait included, because the
   * strip is where the wait is printed too. The profile asks the same question
   * and claims neither, since it answers a failure by leaving the control
   * undrawn and saying nothing.
   */
  it("takes the failed question over, the wait included", async () => {
    eligibility({ canCreate: true });

    await render();

    expect(vi.mocked(userApi.getEndorsementEligibility)).toHaveBeenCalledWith(
      "Reader",
      { ownsRefusal: true, ownsRateLimit: true },
    );
  });

  // A limit taken over has to say how long: the title of a 429 names the
  // refusal without the number, and the number is what a reader acts on.
  it("names the seconds when the question hit the limit", async () => {
    vi.mocked(userApi.getEndorsementEligibility).mockResolvedValue({
      data: null,
      error: {
        status: 429,
        title: "Слишком много запросов",
        retryAfterSeconds: 30,
      },
    } as never);

    const wrapper = await render();

    expect(wrapper.find("textarea.endorsement-text").exists()).toBe(false);
    expect(wrapper.find(".error-state").text()).toBe(
      "Слишком много попыток, попробуйте через 30 секунд",
    );
  });

  it("draws no field when the question itself failed", async () => {
    vi.mocked(userApi.getEndorsementEligibility).mockResolvedValue({
      data: null,
      error: { status: 500, title: "" },
    } as never);

    const wrapper = await render();

    expect(wrapper.find("textarea.endorsement-text").exists()).toBe(false);
    expect(wrapper.find(".error-state").exists()).toBe(true);
    // A failed question is not a "no": the reader stays, with the read
    // offered again.
    expect(replace).not.toHaveBeenCalled();
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
      { ownsRefusal: true, ownsRateLimit: true },
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

  /**
   * One refusal, one carrier — and here the carrier is this page.
   *
   * The POST is sent with `ownsRefusal`, set by this page and not by the API
   * method, so the interceptor raises no plate over a 403 and the refusal is
   * printed
   * under the field the text is still sitting in. Wrapping this in
   * `announcedByInterceptor` would leave the press answered by nothing: the
   * form would stand unchanged, with the typed text in it and no word about why
   * it was not accepted.
   */
  it("prints a refused recommendation under the field it was typed in", async () => {
    vi.mocked(userApi.createUserEndorsement).mockResolvedValue({
      data: null,
      error: { status: 403, title: "Вы еще на испытательном сроке" },
    } as never);
    const wrapper = await render();

    await type(wrapper, "Держит темп и не бросает сцену");
    await wrapper.find("button.submit").trigger("click");
    await flushPromises();

    expect(wrapper.find("span.field-error").text()).toBe(
      "Вы еще на испытательном сроке",
    );
    expect(push).not.toHaveBeenCalled();
    expect(
      (wrapper.find("textarea.endorsement-text").element as HTMLTextAreaElement)
        .value,
    ).toBe("Держит темп и не бросает сцену");
  });

  /**
   * A press refused by the rate limiter is answered in the same line as every
   * other refusal of this press, and it names the seconds.
   *
   * The POST carries `ownsRateLimit` beside `ownsRefusal` (the assertion on the
   * arguments above), so the interceptor raises no plate; the sentence is built
   * through the shared wording, the one the interceptor would have raised,
   * because the problem document of a 429 carries the refusal without the
   * number. Printing the title instead would leave the reader pressing again
   * with nothing to wait for.
   */
  it("prints the wait with the seconds the server named", async () => {
    vi.mocked(userApi.createUserEndorsement).mockResolvedValue({
      data: null,
      error: {
        status: 429,
        title: "Слишком много запросов",
        retryAfterSeconds: 30,
      },
    } as never);
    const wrapper = await render();

    await type(wrapper, "Держит темп и не бросает сцену");
    await wrapper.find("button.submit").trigger("click");
    await flushPromises();

    expect(wrapper.find("span.field-error").text()).toBe(
      "Слишком много попыток, попробуйте через 30 секунд",
    );
    expect(push).not.toHaveBeenCalled();
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
