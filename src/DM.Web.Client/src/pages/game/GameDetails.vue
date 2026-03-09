<script setup lang="ts">
import { computed } from "vue";
import { storeToRefs } from "pinia";
import { useGameDetailsStore } from "@/entities/game";
import { useUserStore } from "@/entities/user";
import { ContentText } from "@/shared/ui";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import { UserLink } from "@/entities/user";
import { GameRole } from "@/entities/game";

const gameStore = useGameDetailsStore();
const { game, rooms, characters } = storeToRefs(gameStore);
const { user } = storeToRefs(useUserStore());

const canEdit = computed(() => {
  if (!game.value?.roles) return false;
  return (
    game.value.roles.includes(GameRole.Master) ||
    game.value.roles.includes(GameRole.Mentor) ||
    game.value.roles.includes(GameRole.Assistant)
  );
});

const activePlayers = computed(() => {
  if (!game.value?.activeCharacterUserIds) return [];
  return game.value.activeCharacterUserIds;
});

const activeCharacters = computed(() => {
  return characters.value.filter((c) => c.status === "Active");
});

const tags = computed(() => game.value?.tags ?? []);
</script>

<template>
  <div v-if="game" class="game-details">
    <!-- Game Description -->
    <section class="game-section" v-if="game.info">
      <block-title>Описание игры</block-title>
      <content-text :html="game.info" />
    </section>

    <!-- Noteworthy (master's notes visible to all) -->
    <section class="game-section" v-if="game.notes && canEdit">
      <block-title>Заметки мастера</block-title>
      <content-text :html="game.notes" />
    </section>

    <!-- Game Stats -->
    <section class="game-section game-stats">
      <block-title>Статистика</block-title>
      <div class="stats-grid">
        <div class="stat-item">
          <span class="stat-label">Комнат:</span>
          <span class="stat-value">{{ rooms.length }}</span>
        </div>
        <div class="stat-item">
          <span class="stat-label">Активных персонажей:</span>
          <span class="stat-value">{{ activeCharacters.length }}</span>
        </div>
        <div class="stat-item">
          <span class="stat-label">Игроков:</span>
          <span class="stat-value">{{ activePlayers.length }}</span>
        </div>
      </div>
    </section>

    <!-- Tags -->
    <section class="game-section" v-if="tags.length">
      <block-title>Теги</block-title>
      <div class="tags-list">
        <span v-for="tag in tags" :key="tag.id" class="tag">
          {{ tag.title }}
        </span>
      </div>
    </section>

    <!-- Privacy Settings (for participants) -->
    <section class="game-section" v-if="canEdit && game.privacySettings">
      <block-title>Настройки приватности</block-title>
      <div class="privacy-list">
        <div class="privacy-item">
          <span class="privacy-label">Показывать характер:</span>
          <span class="privacy-value">{{ game.privacySettings.viewTemper ? "Да" : "Нет" }}</span>
        </div>
        <div class="privacy-item">
          <span class="privacy-label">Показывать историю:</span>
          <span class="privacy-value">{{ game.privacySettings.viewStory ? "Да" : "Нет" }}</span>
        </div>
        <div class="privacy-item">
          <span class="privacy-label">Показывать навыки:</span>
          <span class="privacy-value">{{ game.privacySettings.viewSkills ? "Да" : "Нет" }}</span>
        </div>
        <div class="privacy-item">
          <span class="privacy-label">Показывать инвентарь:</span>
          <span class="privacy-value">{{ game.privacySettings.viewInventory ? "Да" : "Нет" }}</span>
        </div>
        <div class="privacy-item">
          <span class="privacy-label">Показывать приватные сообщения:</span>
          <span class="privacy-value">{{ game.privacySettings.viewPrivates ? "Да" : "Нет" }}</span>
        </div>
        <div class="privacy-item">
          <span class="privacy-label">Показывать броски кубиков:</span>
          <span class="privacy-value">{{ game.privacySettings.viewDice ? "Да" : "Нет" }}</span>
        </div>
      </div>
    </section>

    <!-- Attribute Schema -->
    <section class="game-section" v-if="game.schema">
      <block-title>Система атрибутов</block-title>
      <secondary-text>{{ game.schema.title }}</secondary-text>
    </section>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.game-details
  display: flex
  flex-direction: column
  gap: $big

.game-section
  padding: $medium
  background-color: $bg-element
  border-radius: $border-radius

.stats-grid
  display: grid
  grid-template-columns: repeat(auto-fit, minmax(150px, 1fr))
  gap: $small

.stat-item
  display: flex
  gap: $small

.stat-label
  color: $text-muted

.stat-value
  font-weight: bold

.tags-list
  display: flex
  flex-wrap: wrap
  gap: $small

.tag
  display: inline-block
  padding: 2px $small
  background-color: $bg-element-accent
  border-radius: $border-radius
  font-size: $secondary-font-size
  color: $text-muted

.privacy-list
  display: grid
  grid-template-columns: repeat(auto-fit, minmax(250px, 1fr))
  gap: $small

.privacy-item
  display: flex
  gap: $small

.privacy-label
  color: $text-muted

.privacy-value
  font-weight: bold
</style>
