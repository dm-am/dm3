/**
 * @vitest-environment jsdom
 */

/**
 * The square a camera reads.
 *
 * Three things about it are decisions rather than details, and each is asserted
 * here because each is invisible in the rendered picture.
 *
 * It is dark on white in both themes. That is the one place in the project
 * where a colour is not taken from the palette, approved as a named exception:
 * the dark theme would give a camera a light drawing on a dark field, and part
 * of the cameras will not take one.
 *
 * It is a picture with a name. `uqr`'s own renderSVG has nowhere to put one, so
 * the markup is built here from the module matrix instead - and a reader
 * without a camera hears what the square is and goes looking for the string
 * beside it.
 *
 * And it fails visibly. The package arrives through a dynamic import; when the
 * chunk does not, the box says so rather than staying an empty square forever.
 */
import { describe, expect, it, vi, beforeAll, beforeEach } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import QrCode from "./QrCode.vue";

const URI = "otpauth://totp/Dungeon%20Master:reader?secret=JBSWY3DPEHPK3PXP";

// The package is fetched by a dynamic import, so the first mount waits on a
// module load rather than on a microtask. Warmed here, and the component is
// still polled below: how many turns a resolved import takes is not a promise
// anybody makes.
beforeAll(async () => {
  await import("uqr");
});

async function draw(value = URI) {
  const wrapper = mount(QrCode, {
    props: { value, label: "Код для приложения-аутентификатора" },
  });
  for (let turn = 0; turn < 20; turn++) {
    await flushPromises();
    if (wrapper.find("svg").exists() || wrapper.find(".qr-code-note").exists())
      break;
  }
  return wrapper;
}

describe("QrCode", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("draws the value as a named picture", async () => {
    const wrapper = await draw();

    const svg = wrapper.find("svg");
    expect(svg.exists()).toBe(true);
    expect(svg.attributes("role")).toBe("img");
    expect(svg.attributes("aria-label")).toBe(
      "Код для приложения-аутентификатора",
    );
    expect(wrapper.find("path").attributes("d")).toBeTruthy();
  });

  it("draws dark on white whatever the theme is", async () => {
    const wrapper = await draw();

    expect(wrapper.find("rect").attributes("fill")).toBe("white");
    expect(wrapper.find("path").attributes("fill")).toBe("black");
  });

  // The quiet zone is part of the encoding: the matrix comes back wider than
  // the modules of the code itself, and a camera needs that margin.
  it("keeps a quiet zone wider than a single module", async () => {
    const wrapper = await draw();

    const [, , width] = (wrapper.find("svg").attributes("viewBox") ?? "").split(
      " ",
    );
    // Version 1 is 21 modules; anything this URI encodes to is larger still,
    // and the border adds three on each side.
    expect(Number(width)).toBeGreaterThanOrEqual(21 + 6);
  });

  it("says so when there is no square to draw", async () => {
    // A value no version can hold: the encoder throws, and an empty box that
    // never resolves would be the alternative.
    const wrapper = await draw("x".repeat(10_000));

    expect(wrapper.find("svg").exists()).toBe(false);
    expect(wrapper.text()).toContain("Введите секрет вручную");
  });

  it("draws nothing at all for an empty value", async () => {
    const wrapper = await draw("");

    expect(wrapper.find("svg").exists()).toBe(false);
    expect(wrapper.find(".qr-code-pending").exists()).toBe(true);
  });
});
