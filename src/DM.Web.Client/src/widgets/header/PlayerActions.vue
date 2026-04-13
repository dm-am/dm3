<script setup lang="ts">
import { SvgIcon } from "@/shared/ui/Icon";
import { useUserStore } from "@/entities/user";

const userStore = useUserStore();
const { signOut } = userStore;
</script>

<template>
  <router-link :to="{ name: 'messenger' }" data-testid="messages-link">
    <SvgIcon name="envelope" />
    Сообщения
  </router-link>
  |
  <a @click="signOut" data-testid="logout-button"
    >Выйти</a
  >
</template>

<style scoped lang="sass"></style>
