<script setup lang="ts">
// The moves never round-trip the character's server-rendered BbCode values.
import { computed, ref } from "vue";
import { useRouter } from "vue-router";
import { storeToRefs } from "pinia";
import {
  useGameDetailsStore,
  characterStatusLabel,
  characterApi,
  type Character,
  type CharacterStatusTransition,
} from "@/entities/game";
import { useAuthStore } from "@/entities/user";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";
import { ButtonRow } from "@/shared/ui/ButtonRow";
import type { GeneralError } from "@/shared/api/models/common";
import type { RequestOptions } from "@/shared/api";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";

const props = defineProps<{
  character: Character;
  gameId: string;
}>();

const router = useRouter();
const store = useGameDetailsStore();
const { isMaster, isAssistant } = storeToRefs(store);
const { user } = storeToRefs(useAuthStore());

const canManage = computed(() => isMaster.value || isAssistant.value);

const isOwner = computed(
  () =>
    !!props.character.author && props.character.author.id === user.value?.id,
);

interface CharacterAction {
  key: string;
  label: string;
  confirm?: { title: string; message: string; action: string };
  failure: string;
  /** Handed the window's flags when asked in one: the window prints the refusal. */
  run: (options?: RequestOptions) => Promise<{ error: GeneralError | null }>;
}

const STATUS_FAILED = "Не удалось изменить статус персонажа";

const busy = ref(false);
const pending = ref<CharacterAction | null>(null);

// An active character has no caption in the entity, so the section words it itself.
const statusLabel = computed(
  () => characterStatusLabel(props.character) ?? "В игре",
);

async function applyStatus(
  transition: CharacterStatusTransition,
  options?: RequestOptions,
): Promise<{ error: GeneralError | null }> {
  const c = props.character;
  busy.value = true;
  const { error } = await characterApi.changeCharacterStatus(
    c.id,
    transition,
    options,
  );
  busy.value = false;
  if (error) return { error };
  await store.loadCharacters(props.gameId);
  return { error: null };
}

const transitionTo = (transition: CharacterStatusTransition) => ({
  failure: STATUS_FAILED,
  run: (options?: RequestOptions) => applyStatus(transition, options),
});

async function doDelete(
  options?: RequestOptions,
): Promise<{ error: GeneralError | null }> {
  const c = props.character;
  busy.value = true;
  const { error } = await characterApi.deleteCharacter(c.id, options);
  busy.value = false;
  if (error) return { error };
  // Reload first, navigate second, so the deleted card does not show for a beat.
  await store.loadCharacters(props.gameId);
  router.push({ name: "game-characters", params: { id: props.gameId } });
  return { error: null };
}

function applicationActions(c: Character): CharacterAction[] {
  if (c.status !== "UnderReview" || !canManage.value) return [];
  return [
    {
      key: "accept",
      label: "Принять в игру",
      ...transitionTo("Accept"),
    },
    {
      key: "decline",
      label: "Отклонить заявку",
      confirm: {
        title: "Отклонить заявку?",
        message: "Персонаж не попадет в игру",
        action: "Отклонить",
      },
      ...transitionTo("Decline"),
    },
  ];
}

function activeActions(c: Character): CharacterAction[] {
  if (c.status !== "Active") return [];
  const list: CharacterAction[] = [];
  if (canManage.value) {
    list.push({
      key: "dead",
      label: "Отметить погибшим",
      confirm: {
        title: "Отметить персонажа погибшим?",
        message: "Персонаж погибнет и выйдет из игры",
        action: "Отметить",
      },
      ...transitionTo("Kill"),
    });
    list.push({
      key: "exile",
      label: "Изгнать из игры",
      confirm: {
        title: "Изгнать персонажа из игры?",
        message: "Персонаж выйдет из игры",
        action: "Изгнать",
      },
      ...transitionTo("Exile"),
    });
  }
  if (isOwner.value && !c.isNpc) {
    list.push({
      key: "leave",
      label: "Покинуть игру",
      confirm: {
        title: "Покинуть игру?",
        message: "Персонаж выйдет из игры без возможности вернуться",
        action: "Покинуть",
      },
      ...transitionTo("Leave"),
    });
  }
  return list;
}

// A lead undoes only his own move; what the player took out nobody brings back.
function retiredActions(c: Character): CharacterAction[] {
  if (c.status !== "Retired" || !canManage.value || c.isPlayerLeft) return [];
  if (c.isDead) {
    return [
      {
        key: "resurrect",
        label: "Вернуть в игру",
        ...transitionTo("Resurrect"),
      },
    ];
  }
  if (c.isPlayerExiled) {
    return [
      {
        key: "readmit",
        label: "Вернуть в игру",
        ...transitionTo("Readmit"),
      },
    ];
  }
  return [];
}

function deletionActions(c: Character): CharacterAction[] {
  if (!isOwner.value && !(canManage.value && c.isNpc)) return [];
  return [
    {
      key: "delete",
      label: c.isNpc ? "Удалить неигрового персонажа" : "Удалить персонажа",
      confirm: {
        title: c.isNpc ? "Удалить неигрового персонажа?" : "Удалить персонажа?",
        message: c.isNpc
          ? "Неигровой персонаж будет удален без возможности восстановления"
          : "Персонаж будет удален без возможности восстановления",
        action: "Удалить",
      },
      failure: "Не удалось удалить персонажа",
      run: doDelete,
    },
  ];
}

const actions = computed<CharacterAction[]>(() => {
  const c = props.character;
  return [
    ...applicationActions(c),
    ...activeActions(c),
    ...retiredActions(c),
    ...deletionActions(c),
  ];
});

const statusResult = useActionResult();

async function trigger(action: CharacterAction) {
  statusResult.clear();
  if (action.confirm) {
    pending.value = action;
    return;
  }
  const { error } = await action.run();
  if (error) statusResult.refuse(error, action.failure);
}

async function confirmPending() {
  return pending.value?.run(OWNS_REFUSAL);
}
</script>

<template>
  <section class="status-section">
    <block-title>Статус персонажа</block-title>
    <secondary-text class="status-value">
      {{ statusLabel }}
    </secondary-text>
    <ButtonRow v-if="actions.length" class="status-actions">
      <button
        v-for="action in actions"
        :key="action.key"
        type="button"
        class="status-btn"
        :disabled="busy"
        @click="trigger(action)"
      >
        {{ action.label }}
      </button>
    </ButtonRow>
    <ActionResult :result="statusResult" />

    <!-- Inside the section, so the component is one element; carried to <body> all the same. -->
    <ConfirmDialog
      :show="!!pending"
      :title="pending?.confirm?.title ?? ''"
      :message="pending?.confirm?.message ?? ''"
      :confirm-label="pending?.confirm?.action"
      :failure="pending?.failure"
      :action="confirmPending"
      @update:show="(shown) => !shown && (pending = null)"
    />
  </section>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Buttons" as *

.status-section
  margin-top: $big
  padding: $medium
  background-color: $bg-element

.status-actions
  margin-top: $small

.status-btn
  font-size: $secondary-font-size
  +button
</style>
