<script setup lang="ts">
// The sheet of one character, read-only: CharacterForm in its view mode, so it
// keeps the order of the edit form. A notepad nobody may read on the server is
// left out rather than refused: its caption alone would say too much.
import { computed, ref, watch } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import {
  characterStatusLabel,
  characterApi,
  gameNotepadApi,
  useGameDetailsStore,
  type Character,
} from "@/entities/game";
import { UserLink, useAuthStore } from "@/entities/user";
import { CharacterForm } from "@/features/edit-character";
import { CharacterManageLink } from "@/features/game-actions";
import { AvatarImg } from "@/shared/ui/AvatarImg";
import { ErrorState } from "@/shared/ui/ErrorState";
import { BackLink } from "@/shared/ui/Layout";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import {
  CharacterCardSkeleton,
  CharacterSheetSkeleton,
} from "@/shared/ui/Skeleton";
import { NotepadBoard, adapterOf } from "@/widgets/notepad";
import { useZoneSection } from "@/shared/lib/composables/useZoneSection";
import { useGuardedRequest } from "@/shared/lib/composables/useGuardedRequest";
import { refusalHere } from "@/shared/lib/errors";

const route = useRoute();
const gameStore = useGameDetailsStore();
const { game, isMaster, isAssistant } = storeToRefs(gameStore);
const { user } = storeToRefs(useAuthStore());

const character = ref<Character | null>(null);

const gameId = computed(() => route.params.id as string);
const characterId = computed(() => route.params.characterId as string);

// The name is data, so it cannot sit in meta.section.
useZoneSection(() => character.value?.name);

const schema = computed(() => game.value?.schema ?? { specifications: [] });

const statusLabel = computed(() =>
  character.value ? characterStatusLabel(character.value) : null,
);

const rosterLink = computed(() => ({
  name: "game-characters",
  params: { id: game.value?.publicId || gameId.value },
}));

// The owner alone, the leads excluded (NotepadIntentionResolver, Player).
const ownsCharacter = computed(
  () =>
    !!user.value &&
    !!character.value?.author &&
    character.value.author.id === user.value.id,
);

// The two HasEditAccess admits.
const leadsTheGame = computed(() => isMaster.value || isAssistant.value);

const playerNotesAdapter = adapterOf(() =>
  gameNotepadApi.ofCharacter(characterId.value),
);

const masterNotesAdapter = adapterOf(() =>
  gameNotepadApi.ofCharacterMaster(characterId.value),
);

const LOAD_FAILURE = "Не удалось загрузить персонажа";

// A session that ran out and a request that never left stay on the plate.
const {
  loading,
  error: failure,
  run,
} = useGuardedRequest({
  message: (error) => refusalHere(error, LOAD_FAILURE) ?? LOAD_FAILURE,
});

function load() {
  return run(
    () => characterApi.getCharacter(characterId.value),
    (data) => {
      character.value = data?.resource ?? null;
    },
  );
}

watch(characterId, () => load(), { immediate: true });
</script>

<template>
  <div class="game-character">
    <BackLink :to="rosterLink" label="Назад к персонажам" />

    <div v-if="loading" class="character-loading">
      <CharacterCardSkeleton :count="1" />
      <CharacterSheetSkeleton />
    </div>

    <ErrorState v-else-if="failure" :message="failure" :retry="load" />

    <template v-else-if="character">
      <div class="character-head">
        <AvatarImg
          :picture="character.picture"
          :alt="character.name"
          :size="120"
          img-class="portrait"
          no-default
        />
        <div class="character-meta">
          <SecondaryText v-if="statusLabel" class="meta-line">
            {{ statusLabel }}
          </SecondaryText>
          <SecondaryText v-if="character.author" class="meta-line">
            Игрок: <UserLink :user="character.author" hide-badge />
          </SecondaryText>
          <SecondaryText v-else-if="character.isNpc" class="meta-line">
            Неигровой персонаж
          </SecondaryText>
          <SecondaryText class="meta-line">
            Постов: {{ character.totalPostsCount }}
          </SecondaryText>
          <CharacterManageLink :character="character" />
        </div>
      </div>

      <CharacterForm mode="view" :schema="schema" :character="character" />

      <section v-if="ownsCharacter" class="character-notepad">
        <NotepadBoard
          :adapter="playerNotesAdapter"
          :viewer-id="user?.id ?? null"
          title="Заметки игрока"
          empty-text="Нет записей в заметках игрока"
          empty-hint="Записи о персонаже видите только вы"
          load-error-text="Не удалось загрузить заметки игрока"
        />
      </section>

      <section v-if="leadsTheGame" class="character-notepad">
        <NotepadBoard
          :adapter="masterNotesAdapter"
          :viewer-id="user?.id ?? null"
          :can-delete-others="isMaster"
          title="Заметки мастера"
          empty-text="Нет записей в заметках мастера"
          empty-hint="Записи о персонаже видят только мастер и ассистенты игры"
          load-error-text="Не удалось загрузить заметки мастера"
        />
      </section>
    </template>
  </div>
</template>

<style scoped lang="sass">
.game-character
  min-height: $grid-step * 50

// The head over the sheet, as far apart as .character-head stands from it.
.character-loading
  display: flex
  flex-direction: column
  gap: $medium

.character-head
  display: flex
  align-items: flex-start
  gap: $medium
  margin-bottom: $medium

.portrait
  width: $grid-step * 30
  height: $grid-step * 30
  object-fit: cover

.character-meta
  flex: 1
  min-width: 0

.character-notepad
  margin-top: $big

.meta-line
  & + &
    margin-top: $tiny
</style>
