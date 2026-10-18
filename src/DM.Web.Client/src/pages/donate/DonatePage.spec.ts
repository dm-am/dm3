/**
 * @vitest-environment jsdom
 */

/**
 * "Тестируйте сайт и сообщайте о находках" leads to the forum's "Ошибки".
 *
 * It led to the administration's form with the reason "Ошибка", while the
 * page of support says the opposite in DM2's own words: the form is for the
 * security of the site and for trouble with an account, and every other fault
 * is a topic in "Ошибки", where the community answers (SupportPage.vue).
 */
import { describe, it, expect, vi } from "vitest";
import { mount } from "@vue/test-utils";
import DonatePage from "./DonatePage.vue";

vi.mock("@/entities/fundraising", () => ({
  fundraisingApi: {
    getFundraising: vi.fn(() => new Promise(() => {})),
  },
}));

describe("the page of help to the project", () => {
  it("sends a found fault to the forum's section of faults", () => {
    const wrapper = mount(DonatePage, {
      global: {
        stubs: {
          PageTitle: true,
          BlockTitle: true,
          RouterLink: {
            props: ["to"],
            template:
              "<a class='router-link' :data-to='JSON.stringify(to)'><slot /></a>",
          },
        },
      },
    });

    const link = wrapper
      .findAll(".router-link")
      .find((one) => one.text() === "сообщайте о находках");
    expect(link).toBeDefined();
    expect(JSON.parse(link!.attributes("data-to") ?? "null")).toEqual({
      name: "forum",
      params: { alias: "bugs" },
    });
  });
});
