import { vi } from "vitest";
import { PRIVATE_HOVER_DELAY } from "@/shared/lib/bbcode/bbcodePrivateRecipients";

/**
 * The words of the hidden block's tooltip once the pointer rests on `piece`:
 * "Получатели: DungeonMaster, Assistant, Вася". The caller has turned on fake
 * timers and mounted the piece in the document.
 */
export async function privateTooltipOver(
  piece: Element,
): Promise<string | undefined> {
  piece.dispatchEvent(new MouseEvent("mouseenter"));
  await vi.advanceTimersByTimeAsync(PRIVATE_HOVER_DELAY + 50);
  return document.querySelector('[role="tooltip"]')?.textContent?.trim();
}
