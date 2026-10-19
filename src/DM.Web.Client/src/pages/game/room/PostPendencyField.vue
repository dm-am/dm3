<script setup lang="ts">
// Whose turn the master waits for; each press acts at once, not with the post.
import { computed, ref, watch } from "vue";
import { storeToRefs } from "pinia";
import { roomApi, useGameDetailsStore, type Room } from "@/entities/game";
import type { ApiResult } from "@/shared/api/models/common";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";
import { Select, type SelectOption } from "@/shared/ui/Select";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import Button from "@/shared/ui/Button/Button.vue";
import { symbols } from "@/shared/lib/utils/icons";

const props = defineProps<{
  room: Room | null | undefined;
  gameId: string;
}>();

const gameStore = useGameDetailsStore();
const { characters } = storeToRefs(gameStore);

// What PostPendencyService takes: in play, with an access, with a player.
const awaitableCharacters = computed(() => {
  const admitted = new Set<string>();
  for (const access of props.room?.accesses ?? []) {
    const id = access.character?.id;
    if (id) admitted.add(id);
  }
  return characters.value.filter(
    (c) =>
      c.status === "Active" && !c.isNpc && !!c.author && admitted.has(c.id),
  );
});

const pendencies = computed(() => props.room?.pendencies ?? []);
const newPendencyCharacterId = ref("");
const pendencyBusy = ref(false);

const pendencyOptions = computed<SelectOption[]>(() =>
  awaitableCharacters.value.map((c) => ({
    value: c.id,
    label: c.name,
  })),
);

watch(
  pendencyOptions,
  (options) => {
    if (!options.some((o) => o.value === newPendencyCharacterId.value)) {
      newPendencyCharacterId.value = options[0]?.value ?? "";
    }
  },
  { immediate: true },
);

const turnsResult = useActionResult();

async function changeTurns(
  call: () => Promise<ApiResult<unknown>>,
  failure: string,
) {
  pendencyBusy.value = true;
  turnsResult.clear();
  try {
    const { error } = await call();
    if (error) {
      turnsResult.refuse(error, failure);
      return;
    }
    await gameStore.loadRooms(props.gameId);
  } finally {
    pendencyBusy.value = false;
  }
}

function addPendency() {
  const roomId = props.room?.id;
  if (!newPendencyCharacterId.value || pendencyBusy.value || !roomId) return;
  return changeTurns(
    () =>
      roomApi.createPendency(roomId, {
        characterId: newPendencyCharacterId.value,
      }),
    "Не удалось добавить ожидание хода",
  );
}

function dismissPendency(pendencyId: string) {
  if (pendencyBusy.value) return;
  return changeTurns(
    () => roomApi.deletePendency(pendencyId),
    "Не удалось снять ожидание хода",
  );
}
</script>

<template>
  <div class="turns">
    <div class="turns-title">Я жду хода от:</div>
    <ul v-if="pendencies.length" class="pending-list">
      <li v-for="p in pendencies" :key="p.id" class="pending-item">
        <span class="pending-name">{{ p.characterName }}</span>
        <button
          type="button"
          class="pending-dismiss"
          :disabled="pendencyBusy"
          aria-label="Снять ожидание хода"
          @click="dismissPendency(p.id)"
        >
          {{ symbols.close }}
        </button>
      </li>
    </ul>
    <secondary-text v-else class="turns-empty">
      Сейчас ничьего хода не ждут
    </secondary-text>
    <div v-if="pendencyOptions.length" class="pending-add">
      <Select
        v-model="newPendencyCharacterId"
        :options="pendencyOptions"
        class="pending-select"
        aria-label="Персонаж, от которого ждут хода"
      />
      <Button
        type="button"
        class="pending-add-btn"
        :disabled="pendencyBusy || !newPendencyCharacterId"
        @click="addPendency"
      >
        Ждать хода
      </Button>
    </div>
    <ActionResult :result="turnsResult" />
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *

.turns
  margin-top: $medium

.turns-title
  margin-bottom: $small
  +field-caption

.turns-empty
  margin-bottom: $small

.pending-list
  list-style: none
  display: flex
  flex-wrap: wrap
  gap: $small
  margin: 0 0 $small

.pending-item
  display: inline-flex
  align-items: center
  gap: $tiny
  padding: $tiny $small
  background-color: $bg-element
  border: 1px solid $border
  border-radius: $border-radius

.pending-name
  color: $text

.pending-dismiss
  display: inline-flex
  align-items: center
  justify-content: center
  padding: 0 $tiny
  border: none
  background: transparent
  color: $text-muted
  font-size: $font-size
  line-height: 1
  cursor: pointer
  &:hover:not(:disabled)
    color: $accent-red
  &:disabled
    opacity: $disabled-opacity
    cursor: default

.pending-add
  display: flex
  flex-wrap: wrap
  align-items: center
  gap: $small

// As wide as its longest name, never wider than the column.
.pending-add .pending-select
  width: auto
  max-width: 100%
  min-width: 0

.pending-add-btn
  flex-shrink: 0
</style>
