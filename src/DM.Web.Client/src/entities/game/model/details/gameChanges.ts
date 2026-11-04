// A change answers the refusal or null, and reloads the game on success.

import type { Ref } from "vue";
import type {
  Game,
  GameStatusTransition,
  GamePremoderationTransition,
} from "../gameTypes";
import type { GeneralError } from "@/shared/api/models/common";
import gameApi from "../../api/gameApi";
import { requestNotSent } from "@/shared/lib/errors";
// Deleting a game drops the list caches; the list store must not import this one back.
import { useGamesStore } from "../store";
import type { RequestOptions } from "@/shared/api/interceptor";

type LoadGame = (id: string) => Promise<unknown>;

export function createGameChanges(game: Ref<Game | null>, loadGame: LoadGame) {
  async function transitionStatus(
    transition: GameStatusTransition,
    options?: RequestOptions,
  ): Promise<GeneralError | null> {
    if (!game.value) return requestNotSent;
    const id = game.value.id;
    const { error } = await gameApi.transitionStatus(id, transition, options);
    if (error) return error;
    await loadGame(id);
    return null;
  }

  async function changePremoderation(
    transition: GamePremoderationTransition,
    options?: RequestOptions,
  ): Promise<GeneralError | null> {
    if (!game.value) return requestNotSent;
    const id = game.value.id;
    const { error } = await gameApi.changePremoderation(
      id,
      transition,
      options,
    );
    if (error) return error;
    await loadGame(id);
    return null;
  }

  async function resetRecruitment(
    options?: RequestOptions,
  ): Promise<GeneralError | null> {
    if (!game.value) return requestNotSent;
    const id = game.value.id;
    const { error } = await gameApi.resetRecruitment(id, options);
    if (error) return error;
    await loadGame(id);
    return null;
  }

  async function deleteGame(
    options?: RequestOptions,
  ): Promise<GeneralError | null> {
    if (!game.value) return requestNotSent;
    const { error } = await gameApi.deleteGame(game.value.id, options);
    if (error) return error;
    // Without this the deleted game stays in every list.
    await useGamesStore().invalidateGameLists();
    return null;
  }

  return {
    transitionStatus,
    changePremoderation,
    resetRecruitment,
    deleteGame,
  };
}

export function createSubscriptionChanges(
  game: Ref<Game | null>,
  loadGame: LoadGame,
) {
  async function subscribe(): Promise<GeneralError | null> {
    if (!game.value) return requestNotSent;

    const { error } = await gameApi.subscribe(game.value.id);
    if (error) {
      return error;
    }
    // The roles come with the game.
    await loadGame(game.value.id);
    return null;
  }

  async function unsubscribe(): Promise<GeneralError | null> {
    if (!game.value) return requestNotSent;

    const { error } = await gameApi.unsubscribe(game.value.id);
    if (error) {
      return error;
    }
    await loadGame(game.value.id);
    return null;
  }

  return { subscribe, unsubscribe };
}
