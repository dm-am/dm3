/**
 * @vitest-environment jsdom
 */

/**
 * One control draws the status actions of a game and of a blog.
 *
 * BlogStatusButtons and GameStatusButtons were the same 156 lines twice over,
 * apart from the store they read and the noun in three sentences. They are now
 * two wrappers over this component, and the risk a merge like that carries is
 * visual: the sidebar strip and the page button group are markup no assertion
 * about behaviour would notice moving.
 *
 * So the markup is the assertion. The two callers are mounted here the way the
 * features mount them — a blog's transitions and a game's, which differ by the
 * Finish a blog does not have — and the drawn HTML is compared to what the two
 * pages rendered before they were merged, spelled out below. The layer rules
 * keep the feature wrappers out of a shared spec (shared may not import
 * features), so what is checked here is both call shapes rather than both
 * files; the wrappers add no markup of their own.
 */
import { describe, it, expect, vi } from "vitest";
import { mount, flushPromises } from "@vue/test-utils";
import StatusButtons from "./StatusButtons.vue";
import type { StatusTransitionOption } from "./types";

/** The plates raised, one set for the whole file. */
const plate = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));

vi.mock("@/shared/lib/composables/useToast", () => ({
  useToast: () => plate,
}));

/** What features/blog-actions/model/transitions.ts offers an active blog. */
const BLOG: StatusTransitionOption[] = [
  { value: "Freeze", label: "Заморозить блог" },
  { value: "Close", label: "Закрыть блог", danger: true },
];

/** What features/game-actions/model/transitions.ts offers an active game. */
const GAME: StatusTransitionOption[] = [
  { value: "Finish", label: "Завершить игру" },
  { value: "Freeze", label: "Заморозить игру" },
  { value: "Close", label: "Закрыть игру", danger: true },
];

/** Scope ids are compile-time tokens; the styles moved with the markup. */
const markup = (html: string) => html.replace(/ data-v-[0-9a-f]+=""/g, "");

function render(
  transitions: StatusTransitionOption[],
  subject: string,
  variant: "strip" | "button" = "strip",
  apply = vi.fn().mockResolvedValue(null),
) {
  const wrapper = mount(StatusButtons, {
    props: { transitions, subject, variant, apply },
  });
  return { wrapper, apply };
}

describe("StatusButtons", () => {
  it("draws the sidebar strip as the panel had it", () => {
    const { wrapper } = render(BLOG, "блога");

    expect(markup(wrapper.html())).toBe(
      [
        "<!-- Sidebar strip variant -->",
        '<li class="link"><span class="muted" aria-hidden="true">- </span><button type="button" class="strip-action">Заморозить блог</button></li>',
        '<li class="link"><span class="muted" aria-hidden="true">- </span><button type="button" class="strip-action">Закрыть блог</button></li>',
        "<!--teleport start-->",
        "<!--teleport end-->",
      ].join("\n"),
    );
  });

  it("draws the page button group as the settings page had it", () => {
    const { wrapper } = render(GAME, "игры", "button");

    expect(markup(wrapper.html())).toBe(
      [
        "<!-- Sidebar strip variant -->",
        "<!-- Page button-group variant -->",
        '<div class="status-buttons"><button type="button" class="status-btn">Завершить игру</button><button type="button" class="status-btn">Заморозить игру</button><button type="button" class="status-btn">Закрыть игру</button></div>',
        "<!--teleport start-->",
        "<!--teleport end-->",
      ].join("\n"),
    );
  });

  it("draws nothing at all when the status offers no move", () => {
    const { wrapper } = render([], "игры");

    expect(wrapper.html()).toBe("<!--v-if-->");
  });

  it("runs a plain transition on the spot", async () => {
    const { wrapper, apply } = render(BLOG, "блога");

    await wrapper.findAll("button")[0].trigger("click");
    await flushPromises();

    expect(apply).toHaveBeenCalledWith("Freeze");
    expect(document.body.textContent).not.toContain("Статус блога");
  });

  it("asks first about a destructive one, in the words of the module", async () => {
    const { wrapper, apply } = render(GAME, "игры");

    await wrapper.findAll("button")[2].trigger("click");
    await flushPromises();

    expect(apply).not.toHaveBeenCalled();
    const dialog = document.body.textContent ?? "";
    // The heading is the pressed button turned into a question, and the button
    // in the card is its verb alone - the length every confirm button keeps.
    expect(dialog).toContain("Закрыть игру?");
    expect(dialog).toContain("Статус игры сменится сразу");

    const confirmButton = [...document.querySelectorAll("button")].find(
      (button) => button.textContent?.trim() === "Закрыть",
    );
    confirmButton?.click();
    await flushPromises();

    // The question prints a refusal in itself, so the request tells the
    // interceptor to keep quiet about it.
    expect(apply).toHaveBeenCalledWith("Close", {
      ownsRefusal: true,
      ownsRateLimit: true,
    });
    expect(document.body.textContent).not.toContain("Закрыть игру?");
  });

  /**
   * The question used to close before the request went out, and a refusal
   * came back as a plate over a page that no longer showed what was refused.
   * It waits for the answer now and says a refusal in itself.
   */
  it("keeps the question open with a refusal in it", async () => {
    const apply = vi.fn().mockResolvedValue({
      type: "",
      title: "",
      status: 404,
      traceId: "t",
    });
    const { wrapper } = render(GAME, "игры", "strip", apply);

    await wrapper.findAll("button")[2].trigger("click");
    await flushPromises();
    document.body
      .querySelector<HTMLButtonElement>(".dialog-btn-submit")
      ?.click();
    await flushPromises();

    expect(
      document.body.querySelector(".dialog-refusal")?.textContent?.trim(),
    ).toBe("Не удалось изменить статус игры");
    expect(document.body.textContent).toContain("Закрыть игру?");
    wrapper.unmount();
  });

  /**
   * The strip is a row of the sidebar menu, and a button of a sidebar answers
   * a refusal with a plate, not with a line under its row (UI_STANDARDS,
   * "Всплывающие сообщения"). A transition that asks nothing first has no
   * window to say it in either.
   */
  it("answers a refused plain transition with a plate and adds no row", async () => {
    plate.error.mockClear();
    const apply = vi.fn().mockResolvedValue({
      type: "",
      title: "",
      status: 404,
      traceId: "t",
    });
    const { wrapper } = render(BLOG, "блога", "strip", apply);

    await wrapper.findAll("button")[0].trigger("click");
    await flushPromises();

    expect(plate.error).toHaveBeenCalledWith(
      "Не удалось изменить статус блога",
    );
    expect(wrapper.findAll("li")).toHaveLength(BLOG.length);
    wrapper.unmount();
  });

  it("disables the pressed action while the server is answering", async () => {
    let release = () => {};
    const apply = vi.fn(
      () =>
        new Promise<null>((resolve) => {
          release = () => resolve(null);
        }),
    );
    const { wrapper } = render(BLOG, "блога", "strip", apply);

    await wrapper.findAll("button")[0].trigger("click");
    await flushPromises();

    expect(wrapper.findAll("button")[0].attributes("disabled")).toBe("");
    expect(wrapper.findAll("button")[1].attributes("disabled")).toBeUndefined();

    release();
    await flushPromises();
    expect(wrapper.findAll("button")[0].attributes("disabled")).toBeUndefined();
  });
});
