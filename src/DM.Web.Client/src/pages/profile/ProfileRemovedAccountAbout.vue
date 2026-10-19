<script setup lang="ts">
/**
 * "О себе" of "Аккаунт удален", which is nobody's: one line on what stays
 * under the name and how a person gets his own posts back through support.
 */
import { supportFor } from "@/entities/ticket";
</script>

<template>
  <section class="profile-removed-account-about">
    <p class="removed-account-note">
      Здесь собрано все, что оставили удаленные аккаунты: их посты, топики и
      игры остаются на своих местах под этой подписью. Если среди них ваши,
      вернуть над ними свое имя можно через
      <router-link :to="supportFor('AccountRestoration')">поддержку</router-link
      >.
    </p>
  </section>
</template>

<style scoped lang="sass">
.removed-account-note
  margin: 0
  color: $text
</style>
