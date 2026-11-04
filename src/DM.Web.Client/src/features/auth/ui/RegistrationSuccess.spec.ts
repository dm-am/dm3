/**
 * @vitest-environment jsdom
 */
import { describe, expect, it, vi } from "vitest";
import { mount } from "@vue/test-utils";
import RegistrationSuccess from "./RegistrationSuccess.vue";

vi.mock("vue-final-modal", async () =>
  (await import("@/shared/lib/testing/vueFinalModalStub")).vueFinalModal(),
);

describe("RegistrationSuccess", () => {
  it("names the address at the end of the sentence", () => {
    const wrapper = mount(RegistrationSuccess, {
      props: { email: "reader@dm.am" },
    });

    const sentence = wrapper.get(".auth-text").text().replace(/\s+/g, " ");
    expect(sentence).toBe(
      "Мы отправили письмо со ссылкой для активации на reader@dm.am",
    );
    expect(wrapper.get(".auth-text strong").text()).toBe("reader@dm.am");
  });

  it("says how long the link lives in the hours the registration's answer named", () => {
    const wrapper = mount(RegistrationSuccess, {
      props: { email: "reader@dm.am", hours: 30 },
    });

    expect(wrapper.get(".auth-note").text()).toBe(
      "Ссылка действительна 30 часов",
    );
  });

  // A number of the client's own could be another link's.
  it("promises no lifetime the answer did not name", () => {
    const wrapper = mount(RegistrationSuccess, {
      props: { email: "reader@dm.am" },
    });

    expect(wrapper.find(".auth-note").exists()).toBe(false);
  });
});
