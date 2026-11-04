// One saved page size serves every paged list, within what the API serves.
import { describe, it, expect, beforeEach } from "vitest";
import { setActivePinia, createPinia } from "pinia";
import { useAuthStore } from "@/shared/stores/auth";
import { usePaging, DEFAULT_PAGE_SIZE, MAX_API_PAGE_SIZE } from "./usePaging";

/** A reader whose saved page size is exactly this. */
function reader(pageSize: number | null) {
  useAuthStore().user = { settings: { pageSize } } as never;
}

describe("usePaging", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  it("serves the default to a guest", () => {
    expect(usePaging().pageSize.value).toBe(DEFAULT_PAGE_SIZE);
  });

  it("serves the default to a reader who never saved a size", () => {
    reader(null);

    expect(usePaging().pageSize.value).toBe(DEFAULT_PAGE_SIZE);
  });

  it("serves the size the reader saved", () => {
    reader(50);

    expect(usePaging().pageSize.value).toBe(50);
  });

  it("follows a size saved while the page is open", () => {
    reader(10);
    const { pageSize } = usePaging();

    reader(40);

    expect(pageSize.value).toBe(40);
  });

  it("asks for no more than the API serves", () => {
    reader(MAX_API_PAGE_SIZE * 2);

    expect(usePaging().pageSize.value).toBe(MAX_API_PAGE_SIZE);
  });
});
