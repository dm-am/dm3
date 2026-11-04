/**
 * @vitest-environment jsdom
 */

/**
 * The screen that writes a recommendation: the field only where the server
 * said the POST would be taken, a "no" sent to the profile, a landed one to
 * the recipient's received list, and a refusal that costs nothing typed.
 */
import { describe, expect, it, vi, beforeEach, afterEach } from "vitest";
import {
  enableAutoUnmount,
  flushPromises,
  type VueWrapper,
} from "@vue/test-utils";
import type { Router } from "vue-router";
import { mountPage } from "@/shared/lib/testing/mountPage";
import ProfileEndorsementForm from "./ProfileEndorsementForm.vue";

const userApi = vi.hoisted(() => ({
  getUser: vi.fn(),
  getEndorsementEligibility: vi.fn(),
  createUserEndorsement: vi.fn(),
}));

// The network only: the router, the form and the composables of the zone are the real ones.
vi.mock("@/entities/user/api/userApi", () => ({ default: userApi }));

const FORM = "/users/Reader/write-endorsement";

/** The eligibility read answers an Envelope, like every single-resource read. */
const eligibility = (resource: unknown) =>
  userApi.getEndorsementEligibility.mockResolvedValue({
    data: { resource },
    error: null,
  });

enableAutoUnmount(afterEach);

let router: Router;

async function render() {
  const mounted = await mountPage(ProfileEndorsementForm, FORM, {
    routes: [
      ["home", "/"],
      ["write-endorsement", "/users/:username/write-endorsement"],
      ["profile", "/users/:username"],
      ["received-endorsements", "/users/:username/received-endorsements"],
    ],
  });
  router = mounted.router;
  await flushPromises();
  return mounted.wrapper;
}

/** Where the reader is now. */
const address = () => router.currentRoute.value.fullPath;

const field = (wrapper: VueWrapper) =>
  wrapper.find<HTMLTextAreaElement>("textarea#endorsementText");

const sendButton = (wrapper: VueWrapper) =>
  wrapper
    .findAll("button")
    .find((button) => button.text() === "Отправить рекомендацию")!;

/** The line under the field, if it says anything. */
const fieldError = (wrapper: VueWrapper) => wrapper.find(".form-field-error");

async function send(wrapper: VueWrapper, text: string) {
  await field(wrapper).setValue(text);
  await sendButton(wrapper).trigger("click");
  await flushPromises();
}

beforeEach(() => {
  vi.clearAllMocks();
  userApi.getUser.mockResolvedValue({
    data: { username: "Reader" },
    error: null,
  });
});

describe("ProfileEndorsementForm access", () => {
  it("draws the field where the server says the POST would be accepted", async () => {
    eligibility({ canCreate: true });

    const wrapper = await render();

    expect(field(wrapper).exists()).toBe(true);
    expect(address()).toBe(FORM);
  });

  // A "no" means the address was typed: the profile replaces it, so going back does not land here.
  it.each([
    ["Нельзя рекомендовать самого себя"],
    ["Вы уже рекомендовали этого пользователя"],
  ])("takes the reader to the profile on %s", async (reason) => {
    eligibility({ canCreate: false, reason });

    const wrapper = await render();

    expect(field(wrapper).exists()).toBe(false);
    expect(wrapper.text()).not.toContain(reason);
    expect(address()).toBe("/users/Reader");
    router.back();
    await flushPromises();
    expect(address()).not.toBe(FORM);
  });

  /** A failed question stands in the strip above the form with a retry, both flags with it. */
  it("takes the failed question over, the wait included", async () => {
    eligibility({ canCreate: true });

    await render();

    expect(userApi.getEndorsementEligibility).toHaveBeenCalledWith("Reader", {
      ownsRefusal: true,
      ownsRateLimit: true,
    });
  });

  // The title of a 429 has no number, and the number is what a reader acts on.
  it("names the seconds when the question hit the limit", async () => {
    userApi.getEndorsementEligibility.mockResolvedValue({
      data: null,
      error: {
        status: 429,
        title: "Слишком много запросов",
        retryAfterSeconds: 30,
      },
    });

    const wrapper = await render();

    expect(field(wrapper).exists()).toBe(false);
    expect(wrapper.get(".error-state .error-message").text()).toBe(
      "Слишком много попыток, попробуйте через 30 секунд",
    );
  });

  it("draws no field when the question itself failed", async () => {
    userApi.getEndorsementEligibility.mockResolvedValue({
      data: null,
      error: { status: 500, title: "" },
    });

    const wrapper = await render();

    expect(field(wrapper).exists()).toBe(false);
    expect(wrapper.find(".error-state").exists()).toBe(true);
    // A failed question is not a "no": the reader stays, with a retry.
    expect(address()).toBe(FORM);
  });
});

describe("ProfileEndorsementForm submit", () => {
  beforeEach(() => {
    eligibility({ canCreate: true });
  });

  it("sends the text as typed to the recipient named by the URL", async () => {
    userApi.createUserEndorsement.mockResolvedValue({
      data: { id: "e-1" },
      error: null,
    });
    const wrapper = await render();

    await send(wrapper, "  Держит темп и не бросает сцену  ");

    expect(userApi.createUserEndorsement).toHaveBeenCalledWith(
      "Reader",
      { text: "  Держит темп и не бросает сцену  " },
      { ownsRefusal: true, ownsRateLimit: true },
    );
  });

  it("takes the reader to where the recommendation now lives", async () => {
    userApi.createUserEndorsement.mockResolvedValue({
      data: { id: "e-1" },
      error: null,
    });
    const wrapper = await render();

    await send(wrapper, "Держит темп и не бросает сцену");

    expect(address()).toBe("/users/Reader/received-endorsements");
  });

  it("keeps the text and prints the refusal when the pair already has one", async () => {
    userApi.createUserEndorsement.mockResolvedValue({
      data: null,
      error: { status: 409, title: "Вы уже рекомендовали этого пользователя" },
    });
    const wrapper = await render();

    await send(wrapper, "Держит темп и не бросает сцену");

    expect(address()).toBe(FORM);
    expect(fieldError(wrapper).text()).toBe(
      "Вы уже рекомендовали этого пользователя",
    );
    expect(field(wrapper).element.value).toBe("Держит темп и не бросает сцену");
  });

  /** A refused POST is said under the field the text is still in; the page owns the refusal, not the interceptor. */
  it("prints a refused recommendation under the field it was typed in", async () => {
    userApi.createUserEndorsement.mockResolvedValue({
      data: null,
      error: { status: 403, title: "Вы еще на испытательном сроке" },
    });
    const wrapper = await render();

    await send(wrapper, "Держит темп и не бросает сцену");

    expect(fieldError(wrapper).text()).toBe("Вы еще на испытательном сроке");
    expect(address()).toBe(FORM);
    expect(field(wrapper).element.value).toBe("Держит темп и не бросает сцену");
  });

  /** A rate-limited press is said in the same line, with the seconds, in the shared wording. */
  it("prints the wait with the seconds the server named", async () => {
    userApi.createUserEndorsement.mockResolvedValue({
      data: null,
      error: {
        status: 429,
        title: "Слишком много запросов",
        retryAfterSeconds: 30,
      },
    });
    const wrapper = await render();

    await send(wrapper, "Держит темп и не бросает сцену");

    expect(fieldError(wrapper).text()).toBe(
      "Слишком много попыток, попробуйте через 30 секунд",
    );
    expect(address()).toBe(FORM);
    expect(field(wrapper).element.value).toBe("Держит темп и не бросает сцену");
  });

  it("sends a recommendation of one character", async () => {
    userApi.createUserEndorsement.mockResolvedValue({
      data: { id: "e-1" },
      error: null,
    });
    const wrapper = await render();
    expect(wrapper.get(".form-field-hint").text()).toBe(
      "Чем игрок запомнился в совместной игре",
    );

    await send(wrapper, "Я");

    expect(userApi.createUserEndorsement).toHaveBeenCalledWith(
      "Reader",
      { text: "Я" },
      { ownsRefusal: true, ownsRateLimit: true },
    );
  });

  it("sends nothing while the field holds only spaces", async () => {
    const wrapper = await render();

    await send(wrapper, "   ");

    expect(sendButton(wrapper).attributes("disabled")).toBeDefined();
    expect(userApi.createUserEndorsement).not.toHaveBeenCalled();
  });
});
