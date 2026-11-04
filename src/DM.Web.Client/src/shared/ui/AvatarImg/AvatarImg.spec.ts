/**
 * @vitest-environment jsdom
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
