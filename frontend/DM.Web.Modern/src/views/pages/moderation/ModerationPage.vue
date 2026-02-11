<template>
  <page-title>Модерация</page-title>
  <nav class="moderation-nav">
    <router-link :to="{ name: 'moderation' }">Обзор</router-link>
    <router-link :to="{ name: 'moderation-login-changes' }">Запросы на смену имени пользователя</router-link>
  </nav>
  <router-view />
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.moderation-nav
  display: flex
  gap: $medium
  margin-bottom: $medium
  padding-bottom: $small
  border-bottom: 1px solid $border

  a
    color: $link
    text-decoration: none
    padding: $small $medium
    border-radius: $border-radius

    &:hover
      color: $link-hover

    &.router-link-active
      background: $bg-element-accent
      font-weight: bold
</style>
