import { ref } from "vue";
import type { Game } from "../gameTypes";
import gameApi from "../../api/gameApi";
import { unwrapResource } from "@/shared/api";
import { createRequestGuard } from "@/shared/lib/utils/requestGuard";

function gameState() {
  return {
    game: ref<Game | null>(null),
    gameLoading: ref(false),
    gameError: ref<string | null>(null),
    // A deleted game, a private one and a fallen server are three different pages.
    gameErrorStatus: ref<number | null>(null),
    gameGuard: createRequestGuard(),
  };
}

export function createGameSlice() {
  const state = gameState();
  const { game, gameLoading, gameError, gameErrorStatus, gameGuard } = state;

  /** keepCurrentOnError: a background refresh must not replace the page the reader works in. */
  async function loadGame(
    id: string,
    options?: { keepCurrentOnError?: boolean },
  ): Promise<{ ok: boolean; status?: number }> {
    const requestId = gameGuard.next();
    gameLoading.value = true;
    gameError.value = null;
    gameErrorStatus.value = null;

    const { data, error } = await gameApi.getGame(id);

    // A stale reply reports ok, so its equally stale caller raises no error page.
    if (!gameGuard.isCurrent(requestId)) return { ok: true };

    if (error) {
      if (!options?.keepCurrentOnError) {
        gameError.value = "Не удалось загрузить игру";
        gameErrorStatus.value = error.status ?? null;
        game.value = null;
      }
      gameLoading.value = false;
      return { ok: false, status: error.status };
    }
    if (data) {
      game.value = unwrapResource<Game>(data);
    }

    gameLoading.value = false;
    return { ok: true };
  }

  function resetGame(): void {
    game.value = null;
    gameLoading.value = false;
    gameError.value = null;
    gameErrorStatus.value = null;
  }

  return { ...state, loadGame, resetGame };
}
