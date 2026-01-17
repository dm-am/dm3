<script setup lang="ts">
import { IconType } from "@/components/icons/iconType";
import { useUserStore } from "@/stores";

const userStore = useUserStore();
const { signOut } = userStore;
</script>

<template>
  <router-link :to="{ name: 'messenger' }">
    <the-icon :font="IconType.MessagesNoUnread" />
    Сообщения
  </router-link>
  |
  <a @click="signOut"><the-icon :font="IconType.Logout" /> Выйти</a>
</template>

<style scoped lang="sass"></style>
