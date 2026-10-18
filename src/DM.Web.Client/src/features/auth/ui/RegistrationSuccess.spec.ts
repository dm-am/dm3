/**
 * @vitest-environment jsdom
 */

/**
 * The card that ends the registration says where the letter went in the same
 * order as the other receipts of the guest contour: what was sent first, the
 * address last. The address is what the reader checks against the mailbox he
 * is about to open, and in the middle of the sentence it was the part a
 * glance skipped.
 */
import { describe, expect, it, vi } from "vitest";
import { mount } from "@vue/test-utils";
import { defineComponent, h } from "vue";
import RegistrationSuccess from "./RegistrationSuccess.vue";

// The library tier under AuthDialog wants its plugin installed; the card is
// what is read here, so the modal is a plain box around it.
vi.mock("vue-final-modal", () => ({
  VueFinalModal: defineComponent({
    setup:
      (_props, { slots }) =>
      () =>
        h("div", slots.default?.()),
  }),
}));

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
});
