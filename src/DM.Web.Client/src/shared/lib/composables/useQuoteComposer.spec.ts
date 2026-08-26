/**
 * @vitest-environment jsdom
 */

/**
 * The two halves of the Quote action, and the rule that joins them: the button
 * exists where there is a composer to answer in, and what it puts there is what
 * the server sent — nothing the client assembled on its own.
 */
import { describe, it, expect, vi } from "vitest";
import { defineComponent, h, nextTick, type ComputedRef } from "vue";
import { mount } from "@vue/test-utils";
import type {
  ApiResult,
  Envelope,
  QuoteSource,
} from "@/shared/api/models/common";
import { provideQuoteComposer, useQuoteAction } from "./useQuoteComposer";

const toastInfo = vi.fn();
const toastError = vi.fn();

vi.mock("./useToast", () => ({
  useToast: () => ({
    info: toastInfo,
    error: toastError,
    success: vi.fn(),
    warning: vi.fn(),
  }),
}));

function answer(
  source: QuoteSource,
): () => Promise<ApiResult<Envelope<QuoteSource>>> {
  return () => Promise.resolve({ data: { resource: source }, error: null });
}

type Action = {
  canQuote: ComputedRef<boolean>;
  quote: (
    load: () => Promise<ApiResult<Envelope<QuoteSource>>>,
  ) => Promise<void>;
};

/** A page with a composer, and a message inside it that can quote into it. */
function mountWithComposer(options: {
  enabled: boolean;
  insert: (quote: QuoteSource) => void;
}) {
  let action!: Action;

  const Message = defineComponent({
    setup() {
      action = useQuoteAction();
      return () => h("span");
    },
  });

  const Page = defineComponent({
    setup() {
      provideQuoteComposer({
        enabled: () => options.enabled,
        insert: options.insert,
      });
      return () => h(Message);
    },
  });

  const wrapper = mount(Page);
  return { wrapper, action: () => action };
}

/** A page that offers no composer at all. */
function mountWithoutComposer() {
  let action!: Action;

  const Message = defineComponent({
    setup() {
      action = useQuoteAction();
      return () => h("span");
    },
  });

  mount(Message);
  return () => action;
}

describe("useQuoteComposer", () => {
  it("offers no action where there is no composer", () => {
    const action = mountWithoutComposer();
    expect(action().canQuote.value).toBe(false);
  });

  it("offers no action where the composer is closed", () => {
    const { action } = mountWithComposer({ enabled: false, insert: vi.fn() });
    expect(action().canQuote.value).toBe(false);
  });

  it("offers the action where the composer is open", () => {
    const { action } = mountWithComposer({ enabled: true, insert: vi.fn() });
    expect(action().canQuote.value).toBe(true);
  });

  it("puts the server's markup into the composer, unchanged", async () => {
    const insert = vi.fn();
    const { action } = mountWithComposer({ enabled: true, insert });

    const source: QuoteSource = {
      text: '[quote="Вася"]\nчужая реплика\n[/quote]',
      privateTextStripped: false,
    };
    await action().quote(answer(source));
    await nextTick();

    expect(insert).toHaveBeenCalledTimes(1);
    expect(insert.mock.calls[0][0]).toEqual(source);
  });

  it("tells the author when a hidden block was left out", async () => {
    toastInfo.mockClear();
    const { action } = mountWithComposer({ enabled: true, insert: vi.fn() });

    await action().quote(
      answer({ text: "[quote]текст[/quote]", privateTextStripped: true }),
    );
    await nextTick();

    expect(toastInfo).toHaveBeenCalledWith("Скрытый блок в цитату не попал");
  });

  it("says nothing about a hidden block the reader never saw", async () => {
    toastInfo.mockClear();
    const { action } = mountWithComposer({ enabled: true, insert: vi.fn() });

    await action().quote(
      answer({ text: "[quote]текст[/quote]", privateTextStripped: false }),
    );
    await nextTick();

    expect(toastInfo).not.toHaveBeenCalled();
  });

  it("inserts nothing when the quotation did not arrive", async () => {
    const insert = vi.fn();
    const { action } = mountWithComposer({ enabled: true, insert });

    await action().quote(() =>
      Promise.resolve({
        data: null,
        error: { type: "Unknown", title: "", status: 404, traceId: "" },
      }),
    );
    await nextTick();

    expect(insert).not.toHaveBeenCalled();
  });
});
