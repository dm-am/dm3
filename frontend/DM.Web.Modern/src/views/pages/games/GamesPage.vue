<script setup lang="ts">
import PageTitle from "@/components/layout/PageTitle.vue";
</script>

<template>
  <page-title>Игры</page-title>

  <nav class="games-tabs">
    <router-link :to="{ name: 'games-active' }" class="tabs-link" data-testid="tab-active">
      Активные
    </router-link>
    <router-link :to="{ name: 'games-recruiting' }" class="tabs-link" data-testid="tab-recruiting">
      Набор игроков
    </router-link>
    <router-link :to="{ name: 'games-finished' }" class="tabs-link" data-testid="tab-finished">
      Завершённые
    </router-link>
  </nav>

  <router-view />
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.games-tabs
  margin-bottom: $medium

  .tabs-link
    display: inline-block
    margin-right: $medium
    text-transform: uppercase
    font-weight: bold
    color: $link-nav
    text-decoration: none

    &:hover
      color: $link-nav-hover
      text-decoration: underline

    &.router-link-exact-active
      color: $text
      text-decoration: none
      cursor: default
</style>
