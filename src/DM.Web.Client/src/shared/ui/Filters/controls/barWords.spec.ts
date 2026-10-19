/**
 * @vitest-environment jsdom
 */

/** The buttons of the filter bar keep their words at every width. */
import { describe, it, expect } from "vitest";
import { mount } from "@vue/test-utils";
import FilterButton from "./FilterButton.vue";
import SortButton from "./SortButton.vue";

describe("the buttons of the bar", () => {
  it("are named by their words, which stand on them", () => {
    const filter = mount(FilterButton);
    expect(filter.get("button").attributes("aria-label")).toBe("Фильтры");
    expect(filter.get(".filter-btn-label").text()).toBe("Фильтры");

    const sort = mount(SortButton, {
      props: {
        options: [{ value: "created", label: "Дата создания" }],
        sortBy: "created",
        sortOrder: "desc" as const,
      },
    });
    expect(sort.get(".sort-btn").attributes("aria-label")).toBe(
      "Сортировка: Дата создания",
    );
    expect(sort.get(".sort-btn-label").text()).toBe("Дата создания");
  });
});
