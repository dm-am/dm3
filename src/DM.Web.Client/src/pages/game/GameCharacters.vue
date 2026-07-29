<script setup lang="ts">
import { computed, onMounted } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import { useGameDetailsStore, CharacterCard } from "@/entities/game";
import type { Character } from "@/entities/game";
import { useAuthStore } from "@/entities/user";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";

const route = useRoute();
const gameStore = useGameDetailsStore();
const { characters, charactersError, canManage } = storeToRefs(gameStore);
const { user } = storeToRefs(useAuthStore());

const gameId = computed(() => route.params.id as string);

// A character belongs to the current viewer (its owner may see their own
// application even before it is approved).
function isOwn(c: Character): boolean {
  return !!user.value && !!c.author && c.author.id === user.value.id;
}

// Declined applications are not game characters — the endpoint returns them
// but they are never rendered. Under-review applications are split off into a
// dedicated sub-block (leads see all, an owner sees their own). The roster
// proper shows Active + Retired characters, split PC vs NPC.
const rosterCharacters = computed(() =>
  characters.value.filter(
    (c) => c.status === "Active" || c.status === "Retired",
  ),
);

const playerCharacters = computed(() =>
  rosterCharacters.value.filter((c) => !c.isNpc),
);
const npcCharacters = computed(() =>
  rosterCharacters.value.filter((c) => c.isNpc),
);

// Applications awaiting master review. Visible to leads (master / assistant /
// mentor) and to the applying owner for their own submission.
const underReviewCharacters = computed(() =>
  characters.value.filter(
    (c) => c.status === "UnderReview" && (canManage.value || isOwn(c)),
  ),
);

const hasAnything = computed(
  () =>
    playerCharacters.value.length > 0 ||
    npcCharacters.value.length > 0 ||
    underReviewCharacters.value.length > 0,
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

    <div v-else-if="!hasAnything" class="characters-empty">
      <secondary-text>В этой игре пока нет персонажей</secondary-text>
    </div>

    <div v-else class="characters-groups">
      <!-- Player characters (PC) -->
      <section v-if="playerCharacters.length" class="characters-section">
        <h3 class="section-title">Игровые персонажи</h3>
        <div class="characters-grid">
          <character-card
            v-for="character in playerCharacters"
            :key="character.id"
            :character="character"
          />
        </div>
      </section>

      <!-- Non-player characters (NPC) -->
      <section v-if="npcCharacters.length" class="characters-section">
        <h3 class="section-title">Неигровые персонажи</h3>
        <div class="characters-grid">
          <character-card
            v-for="character in npcCharacters"
            :key="character.id"
            :character="character"
          />
        </div>
      </section>

      <!-- Pending applications — leads see all, owner sees own -->
      <section v-if="underReviewCharacters.length" class="characters-section">
        <h3 class="section-title">На рассмотрении</h3>
        <div class="characters-grid">
          <character-card
            v-for="character in underReviewCharacters"
            :key="character.id"
            :character="character"
          />
        </div>
      </section>
    </div>
  </div>
</template>

<style scoped lang="sass">
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
