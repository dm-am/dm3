/**
 * @vitest-environment jsdom
 */

/**
 * The line a place says about the last press made there.
 *
 * It is the one look of a refusal on a page (UI_STANDARDS, "Всплывающие
 * сообщения"): nothing at all while nothing was refused, so a place does not
 * keep an empty line open under every button, and the sentence read out as an
 * alert when it arrives, since the reader's eyes may be on the field and not on
 * the line.
 */
import { describe, expect, it } from "vitest";
import { nextTick } from "vue";
import { mount } from "@vue/test-utils";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import ActionResult from "./ActionResult.vue";

const refused = (status = 404) => ({
  type: "",
  title: "",
  status,
  traceId: "t",
});

describe("the line of a press", () => {
  it("draws nothing while nothing was refused", () => {
    const wrapper = mount(ActionResult, {
      props: { result: useActionResult() },
    });

    expect(wrapper.find("[role=alert]").exists()).toBe(false);
    expect(wrapper.text()).toBe("");
  });

  it("says the refusal of its place as an alert", async () => {
    const result = useActionResult();
    const wrapper = mount(ActionResult, { props: { result } });

    result.refuse(refused(), "Не удалось сохранить комнату");
    await nextTick();

    const line = wrapper.get("[role=alert]");
    expect(line.element.tagName).toBe("P");
    expect(line.text()).toBe("Не удалось сохранить комнату");
    expect(line.classes()).toContain("action-result");
  });

  it("says a line a place keeps itself", () => {
    const wrapper = mount(ActionResult, {
      props: { text: "Пользователь не убран из черного списка" },
    });

    expect(wrapper.get("[role=alert]").text()).toBe(
      "Пользователь не убран из черного списка",
    );
  });

  it("stands as an item of a list where a paragraph cannot", () => {
    const wrapper = mount(ActionResult, {
      props: { text: "Не удалось изменить подписку", tag: "li" },
    });

    expect(wrapper.get("[role=alert]").element.tagName).toBe("LI");
  });
});
