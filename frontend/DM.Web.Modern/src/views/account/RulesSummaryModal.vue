<script setup lang="ts">
import TheLightbox from "@/components/layout/TheLightbox.vue";
import LightboxTitle from "@/components/layout/LightboxTitle.vue";
import TheButton from "@/components/inputs/TheButton.vue";

const emit = defineEmits<{
  (e: "accept"): void;
  (e: "cancel"): void;
}>();
</script>

<template>
  <the-lightbox>
    <lightbox-title>Правила сайта</lightbox-title>

    <p class="intro">
      Добро пожаловать! Прежде чем начать, ознакомьтесь с основными правилами:
    </p>

    <ol class="rules-list">
      <li>Не оскорбляйте других участников</li>
      <li>Мат и контент 18+ оборачивайте в тег <code>[nsfw]</code></li>
      <li>Не обсуждайте политику нигде и никак</li>
      <li>Один человек — один аккаунт</li>
      <li>Не спамьте и не флудите</li>
    </ol>

    <p class="warning">6 штрафных баллов = автоматический бан</p>

    <p class="full-rules">
      <a href="/rules" target="_blank" rel="noopener">Читать полные правила</a>
    </p>

    <div class="controls">
      <the-button @click="emit('accept')">Понятно</the-button>
    </div>
  </the-lightbox>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.intro
  margin: 0 0 $medium
  line-height: 1.5

.rules-list
  margin: $medium 0
  padding-left: $big

  li
    margin: $small 0
    line-height: 1.4

  code
    font-family: monospace
    background: $bg-element-overlay
    border: 1px solid $border
    padding: 1px 6px
    border-radius: 3px
    font-size: 0.85em
    color: $heading

.warning
  margin: $medium 0
  padding: $small $medium
  background: $bg-element-accent
  border-left: 3px solid $accent-red
  font-weight: 500
  color: $text

.full-rules
  margin: $medium 0

  a
    color: $link
    text-decoration: none
    &:hover
      text-decoration: underline

.controls
  display: flex
  gap: $small
  margin: $medium (-$medium) (-$medium)
  padding: $medium
  background-color: $bg-element-accent
  border-radius: 0 0 $border-radius $border-radius
</style>
