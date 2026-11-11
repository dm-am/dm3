/**
 * @vitest-environment jsdom
 */

// The size of a page of the log is the reader's own, from the account settings.
// The rest of the page is ModerationWarnings.spec.ts's.
import { it, expect, vi, afterEach } from "vitest";
import { enableAutoUnmount, flushPromises } from "@vue/test-utils";
import { createPinia } from "pinia";
import { useAuthStore } from "@/shared/stores";
import ModerationWarnings from "./ModerationWarnings.vue";
import { mountPage } from "@/shared/lib/testing/mountPage";

const { getAllWarnings } = vi.hoisted(() => ({ getAllWarnings: vi.fn() }));

// The network only: the router, the store and the windows are the real ones.
// The original as a plain object: its type would name a file inside another
// slice, which the layer rule refuses even as a type.
vi.mock("@/entities/moderation/api/warningApi", async (importOriginal) => ({
  ...(await importOriginal<object>()),
  default: { getAllWarnings },
}));

enableAutoUnmount(afterEach);

it("asks for pages as long as the reader's lists", async () => {
  getAllWarnings.mockResolvedValue({
    data: { resources: [], paging: null },
    error: null,
  });
  const pinia = createPinia();
  useAuthStore(pinia).user = {
    settings: { pageSize: 7 },
  } as never;

  await mountPage(ModerationWarnings, "/moderation/warnings?number=3", {
    routes: [["moderation-warnings", "/moderation/warnings"]],
    pinia,
  });
  await flushPromises();

  expect(getAllWarnings).toHaveBeenCalledWith({ skip: 14, take: 7 });
});
