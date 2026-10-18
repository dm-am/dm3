/**
 * @vitest-environment jsdom
 */

/**
 * The two pages the letters about turning the second factor off point at.
 *
 * They differ on purpose, and the difference is the thing worth a gate. The
 * page that SCHEDULES a removal waits for a press: following it ends every
 * session of the account and starts a seven-day countdown, and links in letters
 * are opened by scanners and previews as well as by people. The page that CALLS
 * a scheduled removal OFF fires on open, because it undoes rather than starts.
 *
 * Neither has a "check this token" call to lean on - the server has none - so a
 * dead link is learnt from the answer to the action. What the server says about
 * such a link says the heading again, so the heading names it once and the line
 * under the heading says why the link no longer works: a heading on its own
 * leaves the reader to guess what happened to a link he followed a minute ago.
 * That line gives way to the server's sentence when the refusal is about
 * something else - a rank, a factor that is not on - and under it stands the
 * one control every dead link of the site carries.
 *
 * The rest of what is gated here is the shape of the card, because on these two
 * pages the shape IS the message. Each screen says its piece in a heading and
 * at most two lines, and every line that was added on top of those pushed the
 * one thing the reader has to act on further down. So the count of body lines
 * is asserted, not just their presence, and so is the absence of a page heading
 * repeating what the card already says about itself. The width these cards ask
 * for is the register in guestCardWidth.spec.ts.
 */
import { describe, expect, it, vi, beforeEach, afterEach } from "vitest";
import { nextTick } from "vue";
import { flushPromises, mount } from "@vue/test-utils";
import type { VueWrapper } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { accountApi } from "@/entities/user";
import { Api, X_DM_ACCOUNT_TOKEN } from "@/shared/api";
import TwoFactorRemovalPage from "./TwoFactorRemovalPage.vue";
import TwoFactorRemovalCancelPage from "./TwoFactorRemovalCancelPage.vue";

// One push for the whole file: every way out of these cards is a router push,
// and a fresh vi.fn() per useRouter() call leaves nothing to read it from.
const { push } = vi.hoisted(() => ({ push: vi.fn() }));

vi.mock("vue-router", () => ({
  useRoute: () => ({ params: {}, query: {} }),
  useRouter: () => ({ push, replace: vi.fn() }),
  RouterLink: { template: "<a><slot /></a>" },
}));

const options = () => ({
  global: {
    plugins: [createPinia()],
    stubs: {
      RouterLink: { template: "<a><slot /></a>" },
    },
  },
});

/** The heading and the body lines of the card, in the order they are read. */
const headline = (wrapper: VueWrapper) => wrapper.get(".auth-card h1").text();
const bodyLines = (wrapper: VueWrapper) =>
  wrapper.findAll(".auth-text, .auth-note").map((line) => line.text());

/**
 * The two halves of the caution box: the fact in bold and the sentence under
 * it. They are read apart because the box is one element and the halves are
 * two different things - what happens, and what can still be done about it.
 */
const boxLines = (wrapper: VueWrapper) => {
  const box = wrapper.get(".auth-warnbox");
  const lead = box.get("b").text();
  return [lead, box.text().slice(lead.length)];
};

beforeEach(() => {
  setActivePinia(createPinia());
  vi.restoreAllMocks();
  // Not a spy, so restoreAllMocks does not reach it.
  push.mockClear();
  // The value arrives in the fragment: never sent to a server, so it is in no
  // access log and in no Referer.
  window.location.hash = "#token=a-token";
});

describe("scheduling a removal from the letter", () => {
  it("waits for a press instead of acting on open", async () => {
    const schedule = vi.spyOn(accountApi, "scheduleTwoFactorRemoval");

    const wrapper = mount(TwoFactorRemovalPage, options());
    await flushPromises();

    expect(schedule).not.toHaveBeenCalled();
    // The press names what it does, and what it does is not the removal: the
    // removal is a week away, and a button called "Отключить" promises the
    // reader an act that will not happen today.
    expect(wrapper.get(".auth-button").text()).toBe("Назначить отключение");
  });

  /**
   * The card is the page: a heading above it would say the same thing twice,
   * one line higher and in the same size and case.
   */
  it("names itself in the card and nowhere above it", async () => {
    const wrapper = mount(TwoFactorRemovalPage, options());
    await flushPromises();

    expect(wrapper.findAll("h1")).toHaveLength(1);
    expect(headline(wrapper)).toBe("Подтвердите отключение");
  });

  /**
   * Both consequences stand in the caution box rather than in the prose of the
   * card: they are the reason the delay exists, and the reader decides on them.
   */
  it("states both consequences before the press, and nothing else", async () => {
    const wrapper = mount(TwoFactorRemovalPage, options());
    await flushPromises();

    expect(boxLines(wrapper)).toEqual([
      "Это завершит все текущие сессии",
      "Отключение двухфакторной аутентификации произойдет через 7 дней",
    ]);
    expect(bodyLines(wrapper)).toEqual([]);
  });

  /**
   * The refusal the server sends for a link that is not a link is the heading
   * of this screen word for word ("Ссылка недействительна или устарела"), and
   * the screen used to print both. What stands under the heading instead is the
   * thing the heading does not say: how a link of this page dies.
   */
  it("names the dead link once and does not echo the server's word for it", async () => {
    vi.spyOn(accountApi, "scheduleTwoFactorRemoval").mockResolvedValue({
      data: null,
      error: { status: 404, title: "Ссылка недействительна или устарела" },
    } as never);

    const wrapper = mount(TwoFactorRemovalPage, options());
    await flushPromises();
    await wrapper.get(".auth-button").trigger("click");
    await flushPromises();

    expect(headline(wrapper)).toBe("Ссылка недействительна");
    expect(bodyLines(wrapper)).toEqual([
      "Возможно ссылка уже была использована или срок ее действия истек",
    ]);
    expect(wrapper.text()).not.toContain("Ссылка недействительна или устарела");
    expect(wrapper.text()).not.toContain("Отключение назначено");
  });

  /**
   * A refusal about anything else is news: the reader cannot work out from the
   * heading that the mailed path is closed for his rank, and it takes the place
   * of the line about links that outlive themselves.
   */
  it("shows a refusal that is not about the link", async () => {
    vi.spyOn(accountApi, "scheduleTwoFactorRemoval").mockResolvedValue({
      data: null,
      error: {
        status: 403,
        title: "Отключение по почте недоступно для вашей роли",
      },
    } as never);

    const wrapper = mount(TwoFactorRemovalPage, options());
    await flushPromises();
    await wrapper.get(".auth-button").trigger("click");
    await flushPromises();

    expect(bodyLines(wrapper)).toEqual([
      "Отключение по почте недоступно для вашей роли",
    ]);
    expect(wrapper.text()).not.toContain("Отключение назначено");
  });

  /**
   * And that sentence stands in one place. The card above keeps every refusal
   * that is not about the link, so the press claims the refusal from the
   * response interceptor - otherwise the same 403 is written twice, once in the
   * card and once in a plate over it, and only one of the two has the button
   * that lets the reader out.
   *
   * Followed through the real service down to the client, because the test
   * above answers in place of the service and never reaches a request at all:
   * the flag is set at the press, carried by the method, and read off the
   * config by the interceptor, and only the whole chain is worth anything. The
   * credential of the link is asserted beside it - the flags are added to the
   * options the header already travels in, and an options object written over
   * instead of added to would send the press with no token on it.
   */
  it("presses with the refusal and the limit taken over from the interceptor", async () => {
    const post = vi
      .spyOn(Api, "post")
      .mockResolvedValue({ data: null, error: null } as never);

    const wrapper = mount(TwoFactorRemovalPage, options());
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

  /**
   * A rate limit is not a dead link. The card used to answer it with the
   * dead-link screen - "Ссылка недействительна" over a link that was fine, the
   * limiter's title with no number under it - while the interceptor's plate
   * above named the seconds. Now the question keeps the screen under its own
   * heading, the wait stands in a line over the button with the seconds running
   * down, and the button opens again when they run out, the way every guest
   * form answers a 429.
   */
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
      const wrapper = mount(TwoFactorRemovalPage, options());
      await flushPromises();
      await wrapper.get(".auth-button").trigger("click");
      await flushPromises();
      return wrapper;
    };

    beforeEach(() => {
      // Only the interval: everything else in this file runs on real time.
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

    // No header, no number: the sentence then names no wait and the button
    // stays live, because a wait this side invented would be a guess enforced
    // on the reader.
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

  /**
   * Nothing broke and nothing was refused: a letter outlived its link. The
   * cross is the mark of a refusal, and it stands on the screens that carry
   * one.
   */
  it("marks a dead link with the cross, as the other dead links are marked", async () => {
    window.location.hash = "";

    const wrapper = mount(TwoFactorRemovalPage, options());
    await flushPromises();

    expect(wrapper.find(".auth-status--error").exists()).toBe(true);
    expect(wrapper.find(".auth-status--warn").exists()).toBe(false);
  });

  /**
   * A dead link has one way out, and it is the one all six of them share. This
   * card used to open the dialog that mails a new letter instead; the reader who
   * still needs one reaches it from the sign-in, where he came from. Owner's
   * decision.
   */
  it("выводит с мертвой ссылки одной кнопкой на главную", async () => {
    window.location.hash = "";

    const wrapper = mount(TwoFactorRemovalPage, options());
    await flushPromises();

    const ways = wrapper.findAll("button");
    expect(ways).toHaveLength(1);
    expect(ways[0].classes()).toContain("auth-button");
    expect(ways[0].text()).toBe("На главную");
    expect(wrapper.text()).not.toContain("Запросить новую ссылку");

    await ways[0].trigger("click");
    expect(push).toHaveBeenCalledWith("/");
  });

  it("does not offer the press when the link carries no value", async () => {
    window.location.hash = "";
    const schedule = vi.spyOn(accountApi, "scheduleTwoFactorRemoval");

    const wrapper = mount(TwoFactorRemovalPage, options());
    await flushPromises();

    expect(schedule).not.toHaveBeenCalled();
    expect(headline(wrapper)).toBe("Ссылка недействительна");
  });

  /**
   * After the press the countdown moves into the same box the question asked
   * in, and the second line changes from the cost of the act to the way out of
   * it. The old card also announced the letter it had just sent and the
   * sessions it had just ended, which is three lines about the past above the
   * one about what the reader can still do.
   */
  it("names the countdown and the way out once the removal is scheduled", async () => {
    vi.spyOn(accountApi, "scheduleTwoFactorRemoval").mockResolvedValue({
      data: null,
      error: null,
    } as never);

    const wrapper = mount(TwoFactorRemovalPage, options());
    await flushPromises();
    await wrapper.get(".auth-button").trigger("click");
    await flushPromises();

    expect(headline(wrapper)).toBe("Отключение назначено");
    // A receipt, in the two-line shape the other receipts use: the fact, then
    // the way out of it in grey. No framed box - that belongs to the question,
    // where the reader still has a choice.
    expect(bodyLines(wrapper)).toEqual([
      "Защита отключится через 7 дней",
      "Вход с кодом автоматически отменит назначенное отключение",
    ]);
    expect(wrapper.find(".auth-warnbox").exists()).toBe(false);
    expect(wrapper.get(".auth-status").classes()).toContain("auth-status--ok");
    expect(wrapper.get(".auth-button").text()).toBe("На главную");
  });

  /**
   * The errand is over on that screen and the offer of support is not part of
   * it: a support line under a card that reports success reads as a hint that
   * something went wrong anyway.
   */
  it("drops the offer of support once the removal is scheduled", async () => {
    vi.spyOn(accountApi, "scheduleTwoFactorRemoval").mockResolvedValue({
      data: null,
      error: null,
    } as never);

    const wrapper = mount(TwoFactorRemovalPage, options());
    await flushPromises();
    expect(wrapper.find(".auth-footnote").exists()).toBe(true);

    await wrapper.get(".auth-button").trigger("click");
    await flushPromises();

    expect(wrapper.find(".auth-footnote").exists()).toBe(false);
  });
});

describe("calling a scheduled removal off", () => {
  /**
   * The card says what it is doing, the way the other pages from letters do. An
   * empty card with a line to support under it is the shape of a broken page,
   * and it stood there at the moment nothing was broken.
   */
  it("says what it is doing while the request is in flight", () => {
    vi.spyOn(accountApi, "cancelTwoFactorRemoval").mockImplementation(
      () => new Promise(() => {}) as never,
    );

    const wrapper = mount(TwoFactorRemovalCancelPage, options());

    expect(wrapper.get(".auth-card").text()).toBe("Проверяем ссылку...");
    expect(wrapper.find("h1").exists()).toBe(false);
    // Help is offered when something has gone wrong, and nothing has yet.
    expect(wrapper.find(".auth-footnote").exists()).toBe(false);
  });

  it("acts on open", async () => {
    const cancel = vi
      .spyOn(accountApi, "cancelTwoFactorRemoval")
      .mockResolvedValue({ data: null, error: null } as never);

    const wrapper = mount(TwoFactorRemovalCancelPage, options());
    await flushPromises();

    expect(cancel).toHaveBeenCalledWith("a-token");
    expect(headline(wrapper)).toBe("Отключение отменено");
    // The same two-line receipt as the page that schedules the removal: the
    // case it is for in ordinary type, the one thing to do about it in grey.
    // Standing alone as a single sentence it was advice out of nowhere, and
    // the reader who called the removal off himself has no reason to touch his
    // mailbox. The framed box belongs to the question, not to the answer.
    expect(bodyLines(wrapper)).toEqual([
      "Отключение запросили не вы?",
      "Если да, то рекомендуем обновить пароль от вашего почтового ящика",
    ]);
    expect(wrapper.find(".auth-warnbox").exists()).toBe(false);
  });

  /**
   * The same heading as on the page the other letter leads to: both answers are
   * a letter that outlived its link, and two names for one answer read as two
   * different answers. The server's own sentence for it says the heading again,
   * and the lines under the heading say only what this page can know. It reads
   * no account, so it does not know whether the factor is on: the line used to
   * say "уже отменено", and on the eighth day, when the nightly pass had taken
   * the factor off and the cancel token was not yet spent, that was untrue.
   * The first line says there is nothing left for this link to cancel, and
   * the grey one says where the state can be read.
   */
  it("names a dead link the way the other page names it", async () => {
    vi.spyOn(accountApi, "cancelTwoFactorRemoval").mockResolvedValue({
      data: null,
      error: {
        status: 410,
        title: "Ссылка недействительна или уже использована",
      },
    } as never);

    const wrapper = mount(TwoFactorRemovalCancelPage, options());
    await flushPromises();

    expect(headline(wrapper)).toBe("Ссылка недействительна");
    expect(bodyLines(wrapper)).toEqual([
      "Возможно ссылка уже была использована или срок ее действия истек",
    ]);
    expect(wrapper.text()).not.toContain("уже использована");
    expect(wrapper.text()).not.toContain("уже отменено");
    expect(wrapper.text()).not.toContain("Отключение отменено");
  });

  it("says the same when the link carries no value", async () => {
    window.location.hash = "";
    const cancel = vi.spyOn(accountApi, "cancelTwoFactorRemoval");

    const wrapper = mount(TwoFactorRemovalCancelPage, options());
    await flushPromises();

    expect(cancel).not.toHaveBeenCalled();
    expect(headline(wrapper)).toBe("Ссылка недействительна");
    expect(bodyLines(wrapper)).toEqual([
      "Возможно ссылка уже была использована или срок ее действия истек",
    ]);
  });
});

/**
 * The one heading these two pages share is also the one heading of the pair
 * that the narrow card cannot hold: measured in the shipped face it asks for
 * 314px, and a narrow page card leaves 302. The register of widths in
 * guestCardWidth.spec.ts says which measure each state declares; what is gated
 * here is the pair, because the heading is one and a card that keeps the narrow
 * measure under it breaks the heading in two lines.
 *
 * Every other state of both pages stays narrow: none of them takes an address
 * or a password, and none lists the causes behind an answer.
 */
describe("the card that names a dead link", () => {
  const deadStates: readonly [string, () => Promise<VueWrapper>][] = [
    [
      "страница назначения",
      async () => {
        window.location.hash = "";
        const wrapper = mount(TwoFactorRemovalPage, options());
        await flushPromises();
        return wrapper as VueWrapper;
      },
    ],
    [
      "страница отмены",
      async () => {
        window.location.hash = "";
        const wrapper = mount(TwoFactorRemovalCancelPage, options());
        await flushPromises();
        return wrapper as VueWrapper;
      },
    ],
  ];

  it.each(deadStates)("keeps the narrow measure on %s", async (_, open) => {
    const wrapper = await open();

    // One heading, one width. The widening answered a heading this page no
    // longer shows; the one it shows now is the heading of four other screens,
    // and all five are narrow.
    expect(headline(wrapper)).toBe("Ссылка недействительна");
    expect(wrapper.get(".auth-card").classes()).toContain("auth-card--compact");
  });

  it("keeps the narrow measure on every other state", async () => {
    vi.spyOn(accountApi, "cancelTwoFactorRemoval").mockResolvedValue({
      data: null,
      error: null,
    } as never);
    vi.spyOn(accountApi, "scheduleTwoFactorRemoval").mockResolvedValue({
      data: null,
      error: null,
    } as never);

    const asking = mount(TwoFactorRemovalPage, options());
    await flushPromises();
    expect(asking.get(".auth-card").classes()).toContain("auth-card--compact");

    await asking.get(".auth-button").trigger("click");
    await flushPromises();
    expect(asking.get(".auth-card").classes()).toContain("auth-card--compact");

    // The value is cleared from the address as it is read, so the second page
    // of this test gets one of its own.
    window.location.hash = "#token=a-token";
    const cancelling = mount(TwoFactorRemovalCancelPage, options());
    await flushPromises();
    expect(cancelling.get(".auth-card").classes()).toContain(
      "auth-card--compact",
    );
  });
});
