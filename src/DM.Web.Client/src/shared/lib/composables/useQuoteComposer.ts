/**
 * The Quote action, in the two halves it is always in.
 *
 * A message and the box that answers it are never the same component: the post
 * is a widget and the composer belongs to the room, the comment is an item in a
 * list and the composer stands under the whole list, the chat message is a row
 * and the composer is the page's. So the page that owns the composer provides
 * one function, and whatever draws a message picks it up — the same shape the
 * forum shell already uses to report a page-level failure upward.
 *
 * What travels is the finished markup, from the server, per message. The client
 * never builds a quotation out of what is on the page: the rendered HTML is a
 * lossy image of the source, and the source of somebody else's message is not
 * something the browser has. Asking the server also means the text is already
 * filtered for whoever is asking, so a block this reader cannot see is absent by
 * construction and not by a check somebody has to remember.
 */
import {
  computed,
  inject,
  provide,
  type ComputedRef,
  type InjectionKey,
} from "vue";
import type {
  ApiResult,
  Envelope,
  QuoteSource,
} from "@/shared/api/models/common";
import { unwrapResource } from "@/shared/api/envelope";
import { notifyFailure } from "@/shared/lib/errors";
import { useToast } from "@/shared/lib/composables/useToast";

/** One sentence for a quotation that did not arrive, wherever it fails. */
const LOAD_FAILURE = "Не удалось получить цитату";

/**
 * The one line the author is told when their own hidden block did not travel.
 *
 * Only somebody who is shown the block on the page is told this — for everybody
 * else the flag is false, and a message about a block they were never meant to
 * know about would be the disclosure the block exists to prevent.
 */
const PRIVATE_STRIPPED = "Скрытый блок в цитату не попал";

type QuoteComposer = {
  /** Puts a quotation into the composer of the page. */
  insert: (quote: QuoteSource) => void;
  /** Whether the reader may write here at all right now. */
  enabled: () => boolean;
};

const QUOTE_COMPOSER: InjectionKey<QuoteComposer> = Symbol("quote-composer");

/**
 * Offer the page's composer to whatever draws messages below it.
 *
 * @param composer - How to insert, and whether writing is open at all
 */
export function provideQuoteComposer(composer: QuoteComposer): void {
  provide(QUOTE_COMPOSER, composer);
}

/**
 * The Quote action for one message.
 *
 * `canQuote` is false wherever there is no composer to answer in — a page
 * without one, a closed topic, a guest — because an action that cannot end in
 * an answer is not worth a control.
 */
export function useQuoteAction(): {
  canQuote: ComputedRef<boolean>;
  quote: (
    load: () => Promise<ApiResult<Envelope<QuoteSource>>>,
  ) => Promise<void>;
} {
  const composer = inject(QUOTE_COMPOSER, null);

  const canQuote = computed(() => composer?.enabled() ?? false);

  async function quote(
    load: () => Promise<ApiResult<Envelope<QuoteSource>>>,
  ): Promise<void> {
    if (!composer) return;

    const { data, error } = await load();
    const source = data ? unwrapResource<QuoteSource>(data) : null;
    if (!source) {
      notifyFailure(
        error ?? { type: "Unknown", title: "", status: 0, traceId: "" },
        LOAD_FAILURE,
      );
      return;
    }

    composer.insert(source);
    if (source.privateTextStripped) {
      useToast().info(PRIVATE_STRIPPED);
    }
  }

  return { canQuote, quote };
}
