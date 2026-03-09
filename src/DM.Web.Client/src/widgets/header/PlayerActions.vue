<script setup lang="ts">
import { IconType } from "@/shared/ui/Icon/iconType";
import { useUserStore } from "@/entities/user";

const userStore = useUserStore();
const { signOut } = userStore;
</script>

<template>
  <router-link :to="{ name: 'messenger' }" data-testid="messages-link">
    <the-icon :font="IconType.MessagesNoUnread" />
    Сообщения
  </router-link>
  |
  <a @click="signOut" data-testid="logout-button"><the-icon :font="IconType.Logout" /> Выйти</a>
</template>

<style scoped lang="sass"></style>
