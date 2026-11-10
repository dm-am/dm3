/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, afterEach } from "vitest";
import { enableAutoUnmount } from "@vue/test-utils";
import ErrorPage from "./ErrorPage.vue";
import { mountPage } from "@/shared/lib/testing/mountPage";

enableAutoUnmount(afterEach);

describe("ErrorPage", () => {
  it("titles the page with the error's title alone", async () => {
    const { wrapper } = await mountPage(ErrorPage, "/missing", {
      routes: [
        ["missing", "/missing"],
        ["home", "/"],
      ],
      props: { code: 404 },
    });

    expect(wrapper.get("h1").text()).toBe("Страница не найдена");
  });
});
