<script setup lang="ts">
/**
 * "О себе" of the profile "Аккаунт удален".
 *
 * The profile is nobody's: it gathers what every removed account left, so
 * there is no text of its own to show and no note to keep about it. What the
 * reader cannot guess is said instead, in one line: the posts under this name
 * stay where they were written, and a person who finds his own among them can
 * have his name put back over them through support, one account at a time.
 * That used to be the page every "Аккаунт удален" signature led to; the
 * signatures lead here now, and this line is what is left of that page.
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
