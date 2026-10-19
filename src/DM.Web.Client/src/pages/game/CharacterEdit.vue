<script setup lang="ts">
// The sheet of a character with its status section below; a selector switches
// between the characters the viewer may edit.
import { computed, onMounted, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { storeToRefs } from "pinia";
import {
  useGameDetailsStore,
  characterApi,
  GameStatus,
  type Character,
} from "@/entities/game";
import { useAuthStore } from "@/entities/user";
import { CharacterForm } from "@/features/edit-character";
import { CharacterStatusSection } from "@/features/game-actions";
import { Select } from "@/shared/ui/Select";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import FormSkeleton from "@/shared/ui/Skeleton/FormSkeleton.vue";
import { useGuardedRequest } from "@/shared/lib/composables/useGuardedRequest";
import { refusalHere } from "@/shared/lib/errors";

const route = useRoute();
const router = useRouter();
const store = useGameDetailsStore();
const { game, characters, charactersLoading, isMaster, isAssistant } =
  storeToRefs(store);
const { user } = storeToRefs(useAuthStore());

const gameId = computed(() => route.params.id as string);
const routeCharacterId = computed(() => route.params.characterId as string);

const canManage = computed(() => isMaster.value || isAssistant.value);

const schema = computed(() => game.value?.schema ?? { specifications: [] });

// The name and a field per attribute: the form the skeleton stands for.
const formFields = computed(() => schema.value.specifications.length + 1);

onMounted(async () => {
  if (!characters.value.length) await store.loadCharacters(gameId.value);
});

// Staff edit every character the roster holds, a player only their own; a
// declined application is in no roster.
const editableCharacters = computed<Character[]>(() =>
  characters.value.filter(
    (c) => canManage.value || c.author?.id === user.value?.id,
  ),
);

const selectOptions = computed(() =>
  editableCharacters.value.map((c) => ({
    value: c.id,
    label: c.name + (c.isNpc ? " (неигровой персонаж)" : ""),
  })),
);

const selected = computed<Character | undefined>(() => {
  const byRoute = editableCharacters.value.find(
    (c) => c.id === routeCharacterId.value,
  );
  return byRoute ?? editableCharacters.value[0];
});

const selectedId = computed(() => selected.value?.id ?? "");

// Kept after the last character is deleted, so the section's confirmation
// window stays mounted and closes with its own fade.
const statusCharacter = computed<Character | undefined>(
  (previous) => selected.value ?? previous,
);

// The address follows a preselected first character.
watch([selected, editableCharacters], () => {
  if (selected.value && selectedId.value !== routeCharacterId.value) {
    router.replace({
      name: "game-character-edit",
      params: { id: gameId.value, characterId: selectedId.value },
    });
  }
});

function switchCharacter(id: string) {
  router.replace({
    name: "game-character-edit",
    params: { id: gameId.value, characterId: id },
  });
}

// The list carries no attributes, and a save without them drops every one the
// reader did not retype: the form is fed from the AuthorEdit read.
const detail = ref<Character | null>(null);

const DETAIL_FAILURE = "Не удалось загрузить персонажа";

// A session that ran out and a request that never left stay on the plate.
const {
  loading: detailLoading,
  error: detailError,
  run,
} = useGuardedRequest({
  message: (error) => refusalHere(error, DETAIL_FAILURE) ?? DETAIL_FAILURE,
  clearErrorOnStart: true,
});

watch(
  selectedId,
  (id) => {
    detail.value = null;
    if (!id) return;
    return run(
      () => characterApi.getCharacterForEdit(id),
      (data) => {
        detail.value = data?.resource ?? null;
      },
    );
  },
  { immediate: true },
);

// A text attribute comes as typed: the read asks for the author_edit audience.
const initialValues = computed<Record<string, string>>(() => {
  const map: Record<string, string> = {};
  for (const attr of detail.value?.attributes ?? []) {
    map[attr.id] = attr.value ?? attr.valueBbText ?? "";
  }
  return map;
});

// The blacklist closes one's own application: editing it is applying again.
const applicationClosed = computed(() => {
  const c = detail.value;
  return (
    !!game.value?.viewerBlacklisted &&
    !!c?.author &&
    c.author.id === user.value?.id &&
    c.status === "UnderReview"
  );
});

// CharacterIntention.Edit on the server.
const canEditFields = computed(() => {
  const c = detail.value;
  if (!c) return false;
  const owned = !!c.author && c.author.id === user.value?.id;
  if (owned && game.value?.status === GameStatus.Active)
    return !applicationClosed.value;
  return canManage.value && (!!c.isNpc || !!c.privacy?.editByMaster);
});

// No plate: the form's own button says "Сохранено".
function onSaved() {
  store.loadCharacters(gameId.value);
}

function onCancel() {
  router.push({ name: "game-characters", params: { id: gameId.value } });
}
</script>

<template>
  <div class="character-edit">
    <!-- An empty roster before it answers is no news -->
    <FormSkeleton
      v-if="charactersLoading && !characters.length"
      :fields="formFields"
    />

    <secondary-text v-else-if="!editableCharacters.length" class="empty">
      Нет персонажей, доступных для редактирования
    </secondary-text>

    <template v-else-if="selected">
      <div v-if="selectOptions.length > 1" class="character-switch">
        <label class="switch-label" for="character-switch">Персонаж:</label>
        <Select
          id="character-switch"
          :model-value="selectedId"
          :options="selectOptions"
          @update:model-value="(v) => switchCharacter(v as string)"
        />
      </div>

      <FormSkeleton v-if="detailLoading" :fields="formFields" />
      <secondary-text v-else-if="detailError" class="error">
        {{ detailError }}
      </secondary-text>
      <!-- No line in place of the form: what the reader may not do is not on the page. -->
      <CharacterForm
        v-else-if="detail && canEditFields"
        :key="selectedId"
        :schema="schema"
        :character="detail"
        :character-id="selectedId"
        :game-id="game?.id ?? gameId"
        :initial-name="detail.name"
        :initial-values="initialValues"
        @saved="onSaved"
        @cancel="onCancel"
      />
    </template>

    <CharacterStatusSection
      v-if="statusCharacter"
      v-show="selected"
      :character="statusCharacter"
      :game-id="gameId"
    />
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *

.character-edit
  max-width: $grid-step * 200

.error
  color: $accent-red

.character-switch
  display: flex
  align-items: center
  gap: $small
  margin-bottom: $medium

.switch-label
  +field-caption
</style>
