/**
 * @vitest-environment jsdom
 */

/**
 * The default silhouette used to be a data URI, and a data URI is its own
 * document: it cannot see the stylesheet of the page showing it. So its two
 * tones were hard-coded and switched by `prefers-color-scheme` — the operating
 * system's theme. This site's theme is switched by hand, by a class on <html>,
 * and the two disagree the moment a reader picks the one their system is not
 * on: a light-grey square on a #222 page, or a dark one on white.
 *
 * Rendered inline it is painted by the cascade, which cannot disagree with
 * itself, and the four greys that had to be chosen by hand went with the URI.
 */
import { describe, it, expect } from "vitest";
import { mount } from "@vue/test-utils";
import AvatarImg from "./AvatarImg.vue";

describe("AvatarImg", () => {
  it("shows the picture when there is one", () => {
    const wrapper = mount(AvatarImg, {
      props: {
        picture: { smallUrl: "https://example.test/s.jpg" },
        alt: "Кто-то",
        size: 48,
      },
    });

    expect(wrapper.find("img").attributes("src")).toBe(
      "https://example.test/s.jpg",
    );
    expect(wrapper.find("svg").exists()).toBe(false);
  });

  it("draws the default silhouette from the cascade, not from a data URI", () => {
    const wrapper = mount(AvatarImg, {
      props: { picture: null, alt: "Кто-то", size: 48 },
    });

    expect(wrapper.find("img").exists()).toBe(false);
    const svg = wrapper.find("svg.default-avatar");
    expect(svg.exists()).toBe(true);
    // The two tones arrive as classes, so the stylesheet fills them from the
    // theme's tokens. A fill written into the markup would be the old defect,
    // and so would a media query about the operating system.
    expect(svg.html()).toContain("default-avatar-bg");
    expect(svg.html()).toContain("default-avatar-fg");
    expect(svg.html()).not.toContain("prefers-color-scheme");
  });

  it("keeps the slot the same size in both branches", () => {
    const svg = mount(AvatarImg, {
      props: { picture: null, alt: "", size: 220 },
    }).find("svg");

    expect(svg.attributes("width")).toBe("220");
    expect(svg.attributes("height")).toBe("220");
  });

  it("declares the picture's own box when the API measured it", () => {
    // width/height is what the browser reserves the slot from, before it has a
    // byte of the picture. The original preserves the uploaded aspect ratio, so
    // a square declaration reserved 220px for a picture 165px tall and left the
    // difference blank until somebody floored the wrapper to hide it.
    const wrapper = mount(AvatarImg, {
      props: {
        picture: {
          originalUrl: "https://example.test/o.jpg",
          originalWidth: 200,
          originalHeight: 150,
        },
        alt: "Кто-то",
        size: 220,
        preferOriginal: true,
      },
    });

    const img = wrapper.find("img");
    expect(img.attributes("width")).toBe("220");
    expect(img.attributes("height")).toBe("165");
  });

  it("falls back to the square when nobody measured the picture", () => {
    // An upload stored before the pipeline recorded its dimensions. A guessed
    // ratio would be worse than the square: the square is at least the shape
    // the slot floors itself to.
    const wrapper = mount(AvatarImg, {
      props: {
        picture: { originalUrl: "https://example.test/o.jpg" },
        alt: "Кто-то",
        size: 220,
        preferOriginal: true,
      },
    });

    const img = wrapper.find("img");
    expect(img.attributes("width")).toBe("220");
    expect(img.attributes("height")).toBe("220");
  });

  it("keeps thumbnails square whatever the original measures", () => {
    // small and medium are square center-crops at the size they are asked for,
    // so the original's ratio says nothing about the box they occupy.
    const wrapper = mount(AvatarImg, {
      props: {
        picture: {
          smallUrl: "https://example.test/s.jpg",
          originalUrl: "https://example.test/o.jpg",
          originalWidth: 200,
          originalHeight: 150,
        },
        alt: "Кто-то",
        size: 48,
      },
    });

    const img = wrapper.find("img");
    expect(img.attributes("width")).toBe("48");
    expect(img.attributes("height")).toBe("48");
  });

  it("draws nothing for a caller that wants no default", () => {
    // Character: no avatar means no image, unlike User.
    const wrapper = mount(AvatarImg, {
      props: { picture: null, alt: "", size: 48, noDefault: true },
    });

    expect(wrapper.find("img").exists()).toBe(false);
    expect(wrapper.find("svg").exists()).toBe(false);
  });
});
