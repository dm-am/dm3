/**
 * @vitest-environment jsdom
 */

/**
 * The two pages of the letters about switching the second factor off: the one
 * that schedules waits for a press, the one that calls off acts on open. A
 * dead link is learnt from the answer; the heading names it once, the line
 * under it says why or gives the server's sentence, and the one control of the
 * dead links stands under it. The count of body lines is checked: on these
 * cards the shape is the message.
 */
import { describe, expect, it, vi, beforeEach, afterEach } from "vitest";
import { nextTick, type Component } from "vue";
import { flushPromises } from "@vue/test-utils";
import type { VueWrapper } from "@vue/test-utils";
import type { Router } from "vue-router";
import { mountPage, type Address } from "@/shared/lib/testing/mountPage";
import { accountApi } from "@/entities/user";
import { Api, X_DM_ACCOUNT_TOKEN } from "@/shared/api";
import TwoFactorRemovalPage from "./TwoFactorRemovalPage.vue";
import TwoFactorRemovalCancelPage from "./TwoFactorRemovalCancelPage.vue";
/** The two pages, and every address their cards lead to. */
const routes: Address[] = [
  ["two-factor-removal", "/two-factor/removal"],
  ["two-factor-removal-cancel", "/two-factor/removal/cancel"],
  ["home", "/"],
  ["support", "/support"],
];

let router: Router;

/** Open a page at its own address, as its letter does. */
const open = async (page: Component) => {
  const own =
    page === TwoFactorRemovalCancelPage
      ? "/two-factor/removal/cancel"
      : "/two-factor/removal";
  const opened = await mountPage(page, own, { routes });
  router = opened.router;
  return opened.wrapper;
};

/** The heading and the body lines of the card, in the order they are read. */
const headline = (wrapper: VueWrapper) => wrapper.get(".auth-card h1").text();
const bodyLines = (wrapper: VueWrapper) =>
  wrapper.findAll(".auth-text, .auth-note").map((line) => line.text());

/** The two halves of the caution box, the fact in bold and the sentence under it. */
const boxLines = (wrapper: VueWrapper) => {
  const box = wrapper.get(".auth-warnbox");
  const lead = box.get("b").text();
  return [lead, box.text().slice(lead.length)];
};

beforeEach(() => {
  vi.restoreAllMocks();
  // The value arrives in the fragment.
  window.location.hash = "#token=a-token";
});

describe("scheduling a removal from the letter", () => {
  it("waits for a press instead of acting on open", async () => {
    const schedule = vi.spyOn(accountApi, "scheduleTwoFactorRemoval");

    const wrapper = await open(TwoFactorRemovalPage);
    await flushPromises();

    expect(schedule).not.toHaveBeenCalled();
    // The press names what it does: the removal itself is a week away.
    expect(wrapper.get(".auth-button").text()).toBe("Назначить отключение");
  });

  /** The card is the page: no heading above it. */
  it("names itself in the card and nowhere above it", async () => {
    const wrapper = await open(TwoFactorRemovalPage);
    await flushPromises();

    expect(wrapper.findAll("h1")).toHaveLength(1);
    expect(headline(wrapper)).toBe("Подтвердите отключение");
  });

  /** Both consequences stand in the caution box: the reader decides on them. */
  it("states both consequences before the press, and nothing else", async () => {
    const wrapper = await open(TwoFactorRemovalPage);
    await flushPromises();

    expect(boxLines(wrapper)).toEqual([
      "Это завершит все текущие сессии",
      "Отключение двухфакторной аутентификации произойдет через 7 дней",
    ]);
    expect(bodyLines(wrapper)).toEqual([]);
  });

  /** The server's sentence for a dead link repeats the heading, so the line says how a link of this page dies. */
  it("names the dead link once and does not echo the server's word for it", async () => {
    vi.spyOn(accountApi, "scheduleTwoFactorRemoval").mockResolvedValue({
      data: null,
      error: { status: 404, title: "Ссылка недействительна или устарела" },
    } as never);

    const wrapper = await open(TwoFactorRemovalPage);
    await flushPromises();
    await wrapper.get(".auth-button").trigger("click");
    await flushPromises();

    expect(headline(wrapper)).toBe("Ссылка недействительна");
    expect(bodyLines(wrapper)).toEqual([
      "Возможно ссылка уже была использована или срок ее действия истек",
    ]);
  });

  /** A refusal about anything else is news, and takes the place of that line. */
  it("shows a refusal that is not about the link", async () => {
    vi.spyOn(accountApi, "scheduleTwoFactorRemoval").mockResolvedValue({
      data: null,
      error: {
        status: 403,
        title: "Отключение по почте недоступно для вашей роли",
      },
    } as never);

    const wrapper = await open(TwoFactorRemovalPage);
    await flushPromises();
    await wrapper.get(".auth-button").trigger("click");
    await flushPromises();

    expect(bodyLines(wrapper)).toEqual([
      "Отключение по почте недоступно для вашей роли",
    ]);
  });

  /**
   * The press takes the refusal over from the interceptor, followed down to the
   * client, and keeps the credential of the link beside the flags.
   */
  it("presses with the refusal and the limit taken over from the interceptor", async () => {
    const post = vi
      .spyOn(Api, "post")
      .mockResolvedValue({ data: null, error: null } as never);

    const wrapper = await open(TwoFactorRemovalPage);
    await flushPromises();
    await wrapper.get(".auth-button").trigger("click");
    await flushPromises();

    expect(post).toHaveBeenCalledWith(
      "account/two-factor/removal/confirm",
      undefined,
      {
        headers: { [X_DM_ACCOUNT_TOKEN]: "a-token" },
        ownsRefusal: true,
        ownsRateLimit: true,
      },
    );
  });

  /** A rate limit keeps the question on screen, the wait over the button, which opens when it runs out. */
  describe("a rate limit on the press", () => {
    const limited = (retryAfterSeconds?: number) =>
      vi.spyOn(accountApi, "scheduleTwoFactorRemoval").mockResolvedValue({
        data: null,
        error: {
          status: 429,
          title: "Слишком много запросов",
          retryAfterSeconds,
        },
      } as never);

    const press = async () => {
      const wrapper = await open(TwoFactorRemovalPage);
      await flushPromises();
      await wrapper.get(".auth-button").trigger("click");
      await flushPromises();
      return wrapper;
    };

    beforeEach(() => {
      // Only the interval: everything else runs on real time.
      vi.useFakeTimers({ toFake: ["setInterval", "clearInterval"] });
    });

    afterEach(() => {
      vi.useRealTimers();
    });

    it("keeps the question on screen and does not call the link dead", async () => {
      limited(30);
      const wrapper = await press();

      expect(headline(wrapper)).toBe("Подтвердите отключение");
      expect(wrapper.text()).not.toContain("Ссылка недействительна");
      expect(wrapper.text()).not.toContain("Слишком много запросов");
      expect(wrapper.get(".auth-button").text()).toBe("Назначить отключение");
    });

    it("names the seconds over the button, shuts it, and runs down", async () => {
      limited(3);
      const wrapper = await press();

      const strip = wrapper.get(".auth-message");
      expect(strip.text()).toBe(
        "Слишком много попыток, попробуйте через 3 секунды",
      );
      expect(strip.classes()).toContain("auth-message--error");
      expect(wrapper.get(".auth-button").attributes("disabled")).toBe("");

      vi.advanceTimersByTime(1000);
      await nextTick();
      expect(strip.text()).toBe(
        "Слишком много попыток, попробуйте через 2 секунды",
      );

      vi.advanceTimersByTime(2000);
      await nextTick();
      expect(wrapper.find(".auth-message").exists()).toBe(false);
      expect(
        wrapper.get(".auth-button").attributes("disabled"),
      ).toBeUndefined();
    });

    // No number named: no wait is invented, and the button stays live.
    it("names no wait when the server named no seconds", async () => {
      limited(undefined);
      const wrapper = await press();

      expect(wrapper.get(".auth-message").text()).toBe(
        "Слишком много попыток, попробуйте позже",
      );
      expect(
        wrapper.get(".auth-button").attributes("disabled"),
      ).toBeUndefined();
    });
  });

  /** A dead link wears the cross, as the other dead links do. */
  it("marks a dead link with the cross, as the other dead links are marked", async () => {
    window.location.hash = "";

    const wrapper = await open(TwoFactorRemovalPage);
    await flushPromises();

    expect(wrapper.find(".auth-status--error").exists()).toBe(true);
    expect(wrapper.find(".auth-status--warn").exists()).toBe(false);
  });

  /** One way out of a dead link, as on every screen that answers one. */
  it("leads home from a dead link with one button", async () => {
    window.location.hash = "";

    const wrapper = await open(TwoFactorRemovalPage);
    await flushPromises();

    const ways = wrapper.findAll("button");
    expect(ways).toHaveLength(1);
    expect(ways[0].classes()).toContain("auth-button");
    expect(ways[0].text()).toBe("На главную");

    await ways[0].trigger("click");
    await flushPromises();
    expect(router.currentRoute.value.fullPath).toBe("/");
  });

  it("does not offer the press when the link carries no value", async () => {
    window.location.hash = "";
    const schedule = vi.spyOn(accountApi, "scheduleTwoFactorRemoval");

    const wrapper = await open(TwoFactorRemovalPage);
    await flushPromises();

    expect(schedule).not.toHaveBeenCalled();
    expect(headline(wrapper)).toBe("Ссылка недействительна");
  });

  /** After the press the countdown and the way out of it, in the receipt's two lines. */
  it("names the countdown and the way out once the removal is scheduled", async () => {
    vi.spyOn(accountApi, "scheduleTwoFactorRemoval").mockResolvedValue({
      data: null,
      error: null,
    } as never);

    const wrapper = await open(TwoFactorRemovalPage);
    await flushPromises();
    await wrapper.get(".auth-button").trigger("click");
    await flushPromises();

    expect(headline(wrapper)).toBe("Отключение назначено");
    // The two-line shape of the other receipts, no framed box.
    expect(bodyLines(wrapper)).toEqual([
      "Двухфакторная аутентификация отключится через 7 дней",
      "Вход с кодом автоматически отменит назначенное отключение",
    ]);
    expect(wrapper.find(".auth-warnbox").exists()).toBe(false);
    expect(wrapper.get(".auth-status").classes()).toContain("auth-status--ok");
    expect(wrapper.get(".auth-button").text()).toBe("На главную");
  });

  /** No offer of support under a card that reports success. */
  it("drops the offer of support once the removal is scheduled", async () => {
    vi.spyOn(accountApi, "scheduleTwoFactorRemoval").mockResolvedValue({
      data: null,
      error: null,
    } as never);

    const wrapper = await open(TwoFactorRemovalPage);
    await flushPromises();
    expect(wrapper.find(".auth-footnote").exists()).toBe(true);

    await wrapper.get(".auth-button").trigger("click");
    await flushPromises();

    expect(wrapper.find(".auth-footnote").exists()).toBe(false);
  });
});

describe("calling a scheduled removal off", () => {
  /** The card says what it is doing while the request is out. */
  it("says what it is doing while the request is in flight", async () => {
    vi.spyOn(accountApi, "cancelTwoFactorRemoval").mockImplementation(
      () => new Promise(() => {}) as never,
    );

    const wrapper = await open(TwoFactorRemovalCancelPage);

    expect(wrapper.get(".auth-card").text()).toBe("Проверяем ссылку...");
    expect(wrapper.find("h1").exists()).toBe(false);
    // No help while nothing has gone wrong.
    expect(wrapper.find(".auth-footnote").exists()).toBe(false);
  });

  it("acts on open", async () => {
    const cancel = vi
      .spyOn(accountApi, "cancelTwoFactorRemoval")
      .mockResolvedValue({ data: null, error: null } as never);

    const wrapper = await open(TwoFactorRemovalCancelPage);
    await flushPromises();

    expect(cancel).toHaveBeenCalledWith("a-token");
    expect(headline(wrapper)).toBe("Отключение отменено");
    // The two-line receipt of the scheduling page, no framed box.
    expect(bodyLines(wrapper)).toEqual([
      "Отключение запросили не вы?",
      "Если да, то рекомендуем обновить пароль от вашего почтового ящика",
    ]);
    expect(wrapper.find(".auth-warnbox").exists()).toBe(false);
  });

  /** A dead link named as the other page names it, saying only what this page can know: it reads no account. */
  it("names a dead link the way the other page names it", async () => {
    vi.spyOn(accountApi, "cancelTwoFactorRemoval").mockResolvedValue({
      data: null,
      error: {
        status: 410,
        title: "Ссылка недействительна или уже использована",
      },
    } as never);

    const wrapper = await open(TwoFactorRemovalCancelPage);
    await flushPromises();

    expect(headline(wrapper)).toBe("Ссылка недействительна");
    expect(bodyLines(wrapper)).toEqual([
      "Возможно ссылка уже была использована или срок ее действия истек",
    ]);
  });

  it("says the same when the link carries no value", async () => {
    window.location.hash = "";
    const cancel = vi.spyOn(accountApi, "cancelTwoFactorRemoval");

    const wrapper = await open(TwoFactorRemovalCancelPage);
    await flushPromises();

    expect(cancel).not.toHaveBeenCalled();
    expect(headline(wrapper)).toBe("Ссылка недействительна");
    expect(bodyLines(wrapper)).toEqual([
      "Возможно ссылка уже была использована или срок ее действия истек",
    ]);
  });
});
