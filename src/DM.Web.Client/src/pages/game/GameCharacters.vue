<script setup lang="ts">
import { computed, onMounted } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import { useGameDetailsStore } from "@/entities/game";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import GameCharacter from "./GameCharacter.vue";
import { CharacterStatus } from "@/entities/game";

const route = useRoute();
const gameStore = useGameDetailsStore();
const { game, characters, charactersLoading, charactersError } = storeToRefs(gameStore);

const gameId = computed(() => route.params.id as string);

// Group characters by status
const activeCharacters = computed(() =>
  characters.value.filter((c) => c.status === CharacterStatus.Active),
);
const registrationCharacters = computed(() =>
  characters.value.filter((c) => c.status === CharacterStatus.Registration),
);
const deadCharacters = computed(() =>
  characters.value.filter((c) => c.status === CharacterStatus.Dead),
);
const leftCharacters = computed(() =>
  characters.value.filter((c) => c.status === CharacterStatus.Left),
);
const declinedCharacters = computed(() =>
  characters.value.filter((c) => c.status === CharacterStatus.Declined),
);

onMounted(() => {
  if (characters.value.length === 0) {
    gameStore.loadCharacters(gameId.value);
  }
});
</script>

<template>
  <div class="game-characters">
    <div v-if="charactersError" class="characters-error">
      {{ charactersError }}
    </div>

    <div v-else-if="characters.length === 0" class="characters-empty">
      <secondary-text>В этой игре пока нет персонажей</secondary-text>
    </div>

    <div v-else class="characters-groups">
      <!-- Active characters -->
      <section v-if="activeCharacters.length" class="characters-section">
        <h3 class="section-title">Активные персонажи</h3>
        <div class="characters-grid">
          <game-character
            v-for="character in activeCharacters"
            :key="character.id"
            :character="character"
          />
        </div>
      </section>

      <!-- Registration (pending approval) -->
      <section v-if="registrationCharacters.length" class="characters-section">
        <h3 class="section-title">На рассмотрении</h3>
        <div class="characters-grid">
          <game-character
            v-for="character in registrationCharacters"
            :key="character.id"
            :character="character"
          />
        </div>
      </section>

      <!-- Dead characters -->
      <section v-if="deadCharacters.length" class="characters-section">
        <h3 class="section-title">Погибшие</h3>
        <div class="characters-grid">
          <game-character
            v-for="character in deadCharacters"
            :key="character.id"
            :character="character"
          />
        </div>
      </section>

      <!-- Left characters -->
      <section v-if="leftCharacters.length" class="characters-section">
        <h3 class="section-title">Покинувшие игру</h3>
        <div class="characters-grid">
          <game-character
            v-for="character in leftCharacters"
            :key="character.id"
            :character="character"
          />
        </div>
      </section>

      <!-- Declined characters -->
      <section v-if="declinedCharacters.length" class="characters-section">
        <h3 class="section-title">Отклоненные</h3>
        <div class="characters-grid">
          <game-character
            v-for="character in declinedCharacters"
            :key="character.id"
            :character="character"
          />
        </div>
      </section>
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.game-characters
  min-height: $grid-step * 50

.characters-error,
.characters-empty
  padding: $big
  text-align: center

.characters-error
  color: $accent-red

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
