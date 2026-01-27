<script setup lang="ts">
import { computed, ref } from "vue";
import type { Character } from "@/api/models/gaming";
import { CharacterStatus, Alignment } from "@/api/models/gaming";
import UserLink from "@/components/community/UserLink.vue";
import SecondaryText from "@/components/layout/SecondaryText.vue";
import ContentMessage from "@/components/content/ContentMessage.vue";
import TheIcon from "@/components/icons/TheIcon.vue";
import { IconType } from "@/components/icons/iconType";
import defaultPicture from "@/assets/images/userpic.png";

const props = defineProps<{
  character: Character;
}>();

const isExpanded = ref(false);

const statusLabels: Record<CharacterStatus, string> = {
  [CharacterStatus.Active]: "Активен",
  [CharacterStatus.Registration]: "На рассмотрении",
  [CharacterStatus.Dead]: "Погиб",
  [CharacterStatus.Left]: "Покинул игру",
  [CharacterStatus.Declined]: "Отклонён",
};

const alignmentLabels: Record<Alignment, string> = {
  [Alignment.LawfulGood]: "Законопослушный добрый",
  [Alignment.NeutralGood]: "Нейтральный добрый",
  [Alignment.ChaoticGood]: "Хаотичный добрый",
  [Alignment.LawfulNeutral]: "Законопослушный нейтральный",
  [Alignment.TrueNeutral]: "Истинно нейтральный",
  [Alignment.ChaoticNeutral]: "Хаотичный нейтральный",
  [Alignment.LawfulEvil]: "Законопослушный злой",
  [Alignment.NeutralEvil]: "Нейтральный злой",
  [Alignment.ChaoticEvil]: "Хаотичный злой",
};

const pictureUrl = computed(() => props.character.pictureUrl || defaultPicture);
const hasDetails = computed(() =>
  props.character.appearance ||
  props.character.temper ||
  props.character.story ||
  props.character.skills ||
  props.character.inventory
);

function toggleExpand() {
  isExpanded.value = !isExpanded.value;
}
</script>

<template>
  <article class="character-card" :class="{ expanded: isExpanded }">
    <!-- Character header -->
    <header class="character-header" @click="toggleExpand">
      <img :src="pictureUrl" :alt="character.name" class="character-picture" />
      <div class="character-info">
        <h4 class="character-name">{{ character.name }}</h4>
        <secondary-text class="character-meta">
          <span v-if="character.race">{{ character.race }}</span>
          <span v-if="character.race && character.class"> / </span>
          <span v-if="character.class">{{ character.class }}</span>
        </secondary-text>
        <secondary-text class="character-author">
          Игрок: <user-link :user="character.author" />
        </secondary-text>
      </div>
      <the-icon
        :font="isExpanded ? IconType.ChevronUp : IconType.ChevronDown"
        class="expand-icon"
      />
    </header>

    <!-- Character details (expanded) -->
    <div v-if="isExpanded && hasDetails" class="character-details">
      <!-- Alignment -->
      <div v-if="character.alignment" class="detail-section">
        <span class="detail-label">Мировоззрение:</span>
        <span class="detail-value">{{ alignmentLabels[character.alignment] }}</span>
      </div>

      <!-- Posts count -->
      <div v-if="character.totalPostsCount" class="detail-section">
        <span class="detail-label">Постов:</span>
        <span class="detail-value">{{ character.totalPostsCount }}</span>
      </div>

      <!-- Appearance -->
      <div v-if="character.appearance" class="detail-section">
        <span class="detail-label">Внешность:</span>
        <content-message :text="character.appearance" class="detail-content" />
      </div>

      <!-- Temper -->
      <div v-if="character.temper" class="detail-section">
        <span class="detail-label">Характер:</span>
        <content-message :text="character.temper" class="detail-content" />
      </div>

      <!-- Story -->
      <div v-if="character.story" class="detail-section">
        <span class="detail-label">История:</span>
        <content-message :text="character.story" class="detail-content" />
      </div>

      <!-- Skills -->
      <div v-if="character.skills" class="detail-section">
        <span class="detail-label">Навыки:</span>
        <content-message :text="character.skills" class="detail-content" />
      </div>

      <!-- Inventory -->
      <div v-if="character.inventory" class="detail-section">
        <span class="detail-label">Инвентарь:</span>
        <content-message :text="character.inventory" class="detail-content" />
      </div>

      <!-- Attributes -->
      <div v-if="character.attributes?.length" class="detail-section">
        <span class="detail-label">Атрибуты:</span>
        <div class="attributes-list">
          <div v-for="attr in character.attributes" :key="attr.id" class="attribute">
            <span class="attr-title">{{ attr.title }}:</span>
            <span class="attr-value">{{ attr.value }}</span>
            <span v-if="attr.modifier" class="attr-modifier">({{ attr.modifier }})</span>
          </div>
        </div>
      </div>
    </div>
  </article>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.character-card
  background-color: $bg-element
  border-radius: $border-radius
  overflow: hidden

.character-header
  display: flex
  align-items: flex-start
  gap: $small
  padding: $small
  cursor: pointer
  transition: background-color 0.2s

  &:hover
    background-color: $bg-element-accent

.character-picture
  width: $grid-step * 15
  height: $grid-step * 15
  object-fit: cover
  border-radius: $border-radius

.character-info
  flex: 1
  min-width: 0

.character-name
  font-weight: bold
  margin: 0
  white-space: nowrap
  overflow: hidden
  text-overflow: ellipsis

.character-meta
  font-size: $secondary-font-size
  margin-top: $tiny

.character-author
  font-size: $secondary-font-size
  margin-top: $tiny

.expand-icon
  color: $text-muted
  align-self: center

.character-details
  padding: $small
  border-top: 1px solid $border
  background-color: $bg-element-accent

.detail-section
  & + &
    margin-top: $small
    padding-top: $small
    border-top: 1px solid $border

.detail-label
  display: block
  font-size: $secondary-font-size
  color: $text-muted
  margin-bottom: $tiny

.detail-value
  font-weight: bold

.detail-content
  font-size: $secondary-font-size

.attributes-list
  display: grid
  grid-template-columns: repeat(auto-fill, minmax(150px, 1fr))
  gap: $tiny

.attribute
  font-size: $secondary-font-size

.attr-title
  color: $text-muted

.attr-value
  font-weight: bold
  margin-left: $tiny

.attr-modifier
  color: $text-muted
  margin-left: $tiny
</style>
