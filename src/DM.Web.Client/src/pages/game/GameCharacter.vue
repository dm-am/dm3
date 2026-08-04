<script setup lang="ts">
/**
 * GameCharacter — the sheet of one character, read-only.
 *
 * The name of a character used to lead to the roster of the whole game, with a
 * `scrollTo` query no roster ever read, so a name meant "the cast of this game"
 * and nothing narrower. The sheet is served per character (GET
 * /v1/characters/{id}) and until now was fetched only by the edit form.
 *
 * The values are drawn by CharacterForm in its `view` mode: the same schema
 * order the edit form lays out, so the two cannot drift. BbCode attributes
 * arrive as server-rendered HTML (Display audience) and go through ContentText
 * inside that component, never through v-html on a raw string
 * (docs/architecture/BBCODE_RENDERING.md).
 *
 * No lifecycle action lives here. Accepting an application, exiling, marking a
 * character dead and deleting an NPC are one screen of their own, and
 * CharacterManageLink is the same way in that the roster uses.
 */
import { computed, ref, watch } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import {
  characterStatusLabel,
  createEmptySchema,
  gameApi,
  useGameDetailsStore,
  type Character,
} from "@/entities/game";
import { UserLink } from "@/entities/user";
import { CharacterForm } from "@/features/edit-character";
import { CharacterManageLink } from "@/features/game-actions";
import { AvatarImg } from "@/shared/ui";
import { ErrorState } from "@/shared/ui/ErrorState";
import { SvgIcon } from "@/shared/ui/Icon";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import {
  joinTitleSegments,
  useDocumentTitle,
} from "@/shared/lib/composables/useDocumentTitle";
import { describeFailure } from "@/shared/lib/errors";

const route = useRoute();
const gameStore = useGameDetailsStore();
const { game } = storeToRefs(gameStore);

const character = ref<Character | null>(null);
const loading = ref(false);
const failure = ref<string | null>(null);

const gameId = computed(() => route.params.id as string);
const characterId = computed(() => route.params.characterId as string);

// The zone shell titles its sub-pages out of meta.section, and a character's
// name is data — so the page composes its own, in the zone's order: the game
// first, the character second.
useDocumentTitle(() =>
  joinTitleSegments(game.value?.title, character.value?.name),
);

// The schema belongs to the game, not to the character: it is what turns the
// stored attribute values into named rows. The shell renders this page only
// once the game has loaded, so the empty schema is a guard and not a state
// anybody sees.
const schema = computed(() => game.value?.schema ?? createEmptySchema());

const statusLabel = computed(() =>
  character.value ? characterStatusLabel(character.value) : null,
);

const rosterLink = computed(() => ({
  name: "game-characters",
  params: { id: game.value?.publicId || gameId.value },
}));

const LOAD_FAILURE = "Не удалось загрузить персонажа";

async function load() {
  const id = characterId.value;
  if (!id) return;
  loading.value = true;
  failure.value = null;
  const { data, error } = await gameApi.getCharacter(id);
  // A late answer for a character the reader has already left.
  if (characterId.value !== id) return;
  loading.value = false;
  if (error) {
    character.value = null;
    failure.value = describeFailure(error, LOAD_FAILURE);
    return;
  }
  // A 200 with nothing in it: no sentence to read out of the server, so the
  // page says the same thing it says about a refused request.
  if (!data?.resource) {
    character.value = null;
    failure.value = LOAD_FAILURE;
    return;
  }
  character.value = data.resource;
}

watch(characterId, () => load(), { immediate: true });
</script>

<template>
  <div class="game-character">
    <router-link :to="rosterLink" class="back-link">
      <SvgIcon name="chevronLeft" />
      Назад к персонажам
    </router-link>

    <!-- Loading twin of the head below: the portrait at its own size, the name
         line and three meta lines beside it, then the rows of the sheet. -->
    <div v-if="loading" class="character-skeleton" aria-hidden="true">
      <div class="skeleton-title" />
      <div class="skeleton-head">
        <div class="skeleton-portrait" />
        <div class="skeleton-meta">
          <div v-for="i in 3" :key="i" class="skeleton-line" />
        </div>
      </div>
      <div v-for="i in 4" :key="i" class="skeleton-row">
        <div class="skeleton-label" />
        <div class="skeleton-value" />
      </div>
    </div>

    <ErrorState v-else-if="failure" :message="failure" :retry="load" />

    <template v-else-if="character">
      <BlockTitle>{{ character.name }}</BlockTitle>

      <div class="character-head">
        <!-- Character: no avatar = no image (differs from the User default
             silhouette). -->
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
            Игрок: <UserLink :user="character.author" />
          </SecondaryText>
          <SecondaryText v-else-if="character.isNpc" class="meta-line">
            НПС
          </SecondaryText>
          <SecondaryText class="meta-line">
            Постов: {{ character.totalPostsCount }}
          </SecondaryText>
          <CharacterManageLink :character="character" />
        </div>
      </div>

      <CharacterForm mode="view" :schema="schema" :character="character" />
    </template>
  </div>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Skeleton"

.game-character
  min-height: $grid-step * 50

.back-link
  display: inline-flex
  align-items: center
  gap: $tiny
  margin-bottom: $medium
  color: $link
  text-decoration: none

  &:hover
    text-decoration: underline

.character-head
  display: flex
  align-items: flex-start
  gap: $medium
  margin-bottom: $medium

.portrait
  width: $grid-step * 30
  height: $grid-step * 30
  object-fit: cover
  border-radius: $border-radius

.character-meta
  flex: 1
  min-width: 0

.meta-line
  display: block

  & + &
    margin-top: $tiny

// Loading twin — the geometry of the head and of the first rows of the sheet.
.character-skeleton
  display: flex
  flex-direction: column
  gap: $medium

.skeleton-head
  display: flex
  align-items: flex-start
  gap: $medium

.skeleton-title
  width: $grid-step * 50
  height: $grid-step * 6
  +skeleton-shimmer

// No border-radius of its own: the shimmer mixin sets one, and it has to be
// called last (a declaration after it trips the sass mixed-decls warning).
.skeleton-portrait
  flex-shrink: 0
  width: $grid-step * 30
  height: $grid-step * 30
  +skeleton-shimmer

.skeleton-meta
  flex: 1
  min-width: 0
  display: flex
  flex-direction: column
  gap: $small

.skeleton-line
  width: 40%
  height: $grid-step * 4
  +skeleton-shimmer

.skeleton-row
  display: flex
  flex-direction: column
  gap: $tiny

.skeleton-label
  width: $grid-step * 25
  height: $grid-step * 3
  +skeleton-shimmer

.skeleton-value
  width: 80%
  height: $grid-step * 4
  +skeleton-shimmer
</style>
