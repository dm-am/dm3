<script setup lang="ts">
// No guest branch: the /messenger subtree is requiresAuth, so the router is
// the one mechanism that turns a guest away (guardAuthenticated).
import { storeToRefs } from "pinia";
import { useMessagingStore } from "@/entities/message";

const messagingStore = useMessagingStore();
const { totalUnreadCount } = storeToRefs(messagingStore);
</script>

<template>
  <page-title>
    Личные сообщения
    <span v-if="totalUnreadCount > 0" class="unread-badge">
      {{ totalUnreadCount }}
    </span>
  </page-title>
  <router-view />
</template>

<style scoped lang="sass">
.unread-badge
  display: inline-block
  padding: 2px 8px
  margin-left: $small
  border-radius: $border-radius
  font-size: $secondary-font-size
  background-color: $button-bg
  color: $button-text
</style>
