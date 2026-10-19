import { computed, type Ref } from "vue";
import type { Game } from "../gameTypes";
import { GameParticipation } from "../gameTypes";

// The participation flags, not role names: Authority is the master or an assistant, Moderator the mentor.
export function createGameRoles(game: Ref<Game | null>) {
  const participation = computed<GameParticipation[]>(
    () => game.value?.participation ?? [],
  );
  const isMaster = computed(() =>
    participation.value.includes(GameParticipation.Owner),
  );
  const isAssistant = computed(
    () =>
      participation.value.includes(GameParticipation.Authority) &&
      !isMaster.value,
  );
  const isMentor = computed(() =>
    participation.value.includes(GameParticipation.Moderator),
  );
  const isSubscribed = computed(() =>
    participation.value.includes(GameParticipation.Reader),
  );
  const isPlayer = computed(
    () =>
      participation.value.includes(GameParticipation.Player) ||
      isMentor.value ||
      isMaster.value,
  );
  const canManage = computed(
    () => isMaster.value || isAssistant.value || isMentor.value,
  );

  return { isMaster, isAssistant, isMentor, isSubscribed, isPlayer, canManage };
}
