<script setup lang="ts">
import { computed, onMounted } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import { useGameDetailsStore, CharacterCard } from "@/entities/game";
import { CharacterManageLink } from "@/features/game-actions";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { ErrorState } from "@/shared/ui/ErrorState";
import { CharacterCardSkeleton } from "@/shared/ui/Skeleton";

const route = useRoute();
const gameStore = useGameDetailsStore();
const { characters, charactersLoading, charactersError } =
  storeToRefs(gameStore);

const gameId = computed(() => route.params.id as string);

// An application under review is everybody's to read; a declined one never
// comes from the server.
const groups = computed(() => {
  const roster = characters.value.filter(
    (c) => c.status === "Active" || c.status === "Retired",
  );
  return [
    { title: "Персонажи игроков", characters: roster.filter((c) => !c.isNpc) },
    { title: "Неигровые персонажи", characters: roster.filter((c) => c.isNpc) },
    {
      title: "На рассмотрении",
      characters: characters.value.filter((c) => c.status === "UnderReview"),
    },
  ].filter((group) => group.characters.length > 0);
});

function loadRoster() {
  return gameStore.loadCharacters(gameId.value);
}

onMounted(() => {
  if (characters.value.length === 0) {
    loadRoster();
  }
});
</script>

<template>
  <div class="game-characters">
    <CharacterCardSkeleton v-if="charactersLoading && !characters.length" />

    <ErrorState
      v-else-if="charactersError"
      :message="charactersError"
      :retry="loadRoster"
    />

    <div v-else-if="!groups.length" class="characters-empty">
      <secondary-text>В этой игре пока нет персонажей</secondary-text>
    </div>

    <div v-else class="characters-groups">
      <section
        v-for="group in groups"
        :key="group.title"
        class="characters-section"
      >
        <h3 class="section-title">{{ group.title }}</h3>
        <div class="characters-grid">
          <character-card
            v-for="character in group.characters"
            :key="character.id"
            :data-id="character.id"
            :character="character"
          >
            <template #controls>
              <CharacterManageLink :character="character" />
            </template>
          </character-card>
        </div>
      </section>
    </div>
  </div>
</template>

<style scoped lang="sass">
.game-characters
  min-height: $grid-step * 50

.characters-empty
  padding: $big

.characters-groups
  display: flex
  flex-direction: column
  gap: $big

.characters-section
  .section-title
    margin-bottom: $medium
    font-size: $font-size
    color: $text-muted
    text-transform: uppercase

.characters-grid
  display: grid
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr))
  gap: $medium
</style>
