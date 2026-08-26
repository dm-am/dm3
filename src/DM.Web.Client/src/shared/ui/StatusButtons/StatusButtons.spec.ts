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

vi.mock("@/shared/lib/composables/useToast", () => ({
  useToast: () => ({ success: vi.fn(), error: vi.fn() }),
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
        '<li class="link"><span class="muted" aria-hidden="true">- </span><button type="button" class="strip-action danger">Закрыть блог</button></li>',
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
        '<div class="status-buttons"><button type="button" class="status-btn">Завершить игру</button><button type="button" class="status-btn">Заморозить игру</button><button type="button" class="status-btn danger">Закрыть игру</button></div>',
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
    expect(document.body.textContent).not.toContain("Изменение статуса");
  });

  it("asks first about a destructive one, in the words of the module", async () => {
    const { wrapper, apply } = render(GAME, "игры");

    await wrapper.findAll("button")[2].trigger("click");
    await flushPromises();

    expect(apply).not.toHaveBeenCalled();
    const dialog = document.body.textContent ?? "";
    expect(dialog).toContain("Изменение статуса игры");
    expect(dialog).toContain(
      'Действие "Закрыть игру" изменит статус игры. Продолжить?',
    );

    const confirmButton = [...document.querySelectorAll("button")].find(
      (button) => button.textContent?.trim() === "Закрыть игру",
    );
    confirmButton?.click();
    await flushPromises();

    expect(apply).toHaveBeenCalledWith("Close");
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
