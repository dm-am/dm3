<script setup lang="ts">
import { computed } from "vue";
import type { Post } from "@/entities/game";
import { ContentText } from "@/shared/ui";
import { UserLink } from "@/entities/user";
import HumanDate from "@/shared/ui/Date/HumanDate.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";

const props = defineProps<{
  post: Post;
}>();

const hasCharacter = computed(() => !!props.post.character);
const hasAuthor = computed(() => !!props.post.author);
const hasDiceRolls = computed(() => props.post.diceRolls && props.post.diceRolls.length > 0);
const hasCommentary = computed(() => !!props.post.commentary?.html);
const hasMasterMessage = computed(() => !!props.post.masterMessage?.html);
</script>

<template>
  <article class="game-post">
    <!-- Post header -->
    <header class="post-header">
      <div class="post-author">
        <!-- Character info -->
        <div v-if="hasCharacter" class="post-character">
          <span class="character-name">{{ post.character!.name }}</span>
          <secondary-text v-if="hasAuthor" class="character-player">
            (<user-link :user="post.author!" />)
          </secondary-text>
        </div>
        <!-- Author only (no character) -->
        <div v-else-if="hasAuthor" class="post-author-only">
          <user-link :user="post.author!" />
        </div>
        <!-- System post -->
        <div v-else class="post-system">
          <secondary-text>Системное сообщение</secondary-text>
        </div>
      </div>
      <div class="post-date">
        <human-date :date="post.createdUtc" />
        <secondary-text v-if="post.updatedUtc" class="post-edited">
          (ред.)
        </secondary-text>
      </div>
    </header>

    <!-- Main text -->
    <div class="post-content">
      <content-text :html="post.text.html" />
    </div>

    <!-- Commentary (OOC text) -->
    <div v-if="hasCommentary" class="post-commentary">
      <secondary-text class="commentary-label">OOC:</secondary-text>
      <content-text :html="post.commentary!.html" />
    </div>

    <!-- Master message (private to master) -->
    <div v-if="hasMasterMessage" class="post-master-message">
      <secondary-text class="master-label">Мастеру:</secondary-text>
      <content-text :html="post.masterMessage!.html" />
    </div>

    <!-- Dice rolls -->
    <div v-if="hasDiceRolls" class="post-dice">
      <div v-for="roll in post.diceRolls" :key="roll.id" class="dice-roll">
        <span class="dice-result">
          d{{ roll.dice }}: {{ roll.result }}
          <span v-if="roll.bonus">{{ roll.bonus > 0 ? '+' : '' }}{{ roll.bonus }}</span>
          <span class="dice-total">= {{ roll.result + (roll.bonus || 0) }}</span>
        </span>
        <secondary-text v-if="roll.comment" class="dice-comment">
          {{ roll.comment }}
        </secondary-text>
      </div>
    </div>
  </article>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.game-post
  padding: $medium
  background-color: $bg-element
  border-radius: $border-radius

.post-header
  display: flex
  justify-content: space-between
  align-items: flex-start
  margin-bottom: $small
  padding-bottom: $small
  border-bottom: 1px solid $border

.post-author
  flex: 1

.post-character
  display: flex
  align-items: baseline
  gap: $small

.character-name
  font-weight: bold
  color: $link

.character-player
  font-size: $secondary-font-size

.post-author-only
  font-weight: bold

.post-system
  font-style: italic

.post-date
  display: flex
  align-items: center
  gap: $tiny
  font-size: $secondary-font-size
  color: $text-muted

.post-edited
  font-size: $tertiary-font-size

.post-content
  margin-bottom: $small

.post-commentary
  margin-top: $small
  padding: $small
  background-color: $bg-element-accent
  border-radius: $border-radius
  border-left: 3px solid $link

.commentary-label
  display: block
  margin-bottom: $tiny
  font-weight: bold
  color: $link

.post-master-message
  margin-top: $small
  padding: $small
  background-color: rgba($accent-red, 0.1)
  border-radius: $border-radius
  border-left: 3px solid $accent-red

.master-label
  display: block
  margin-bottom: $tiny
  font-weight: bold
  color: $accent-red

.post-dice
  margin-top: $small
  padding: $small
  background-color: $bg-element-accent
  border-radius: $border-radius

.dice-roll
  display: flex
  align-items: center
  gap: $small

  & + &
    margin-top: $tiny

.dice-result
  font-family: monospace
  font-size: $secondary-font-size

.dice-total
  font-weight: bold
  color: $accent-green

.dice-comment
  font-style: italic
</style>
