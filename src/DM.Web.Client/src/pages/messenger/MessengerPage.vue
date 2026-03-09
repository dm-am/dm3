<script setup lang="ts">
import { storeToRefs } from "pinia";
import { useUserStore } from "@/entities/user";
import { useMessagingStore } from "@/entities/message";

const { user } = storeToRefs(useUserStore());
const messagingStore = useMessagingStore();
const { totalUnreadCount } = storeToRefs(messagingStore);
</script>

<template>
  <template v-if="user">
    <page-title>
      Личные сообщения
      <span v-if="totalUnreadCount > 0" class="unread-badge">
        {{ totalUnreadCount }}
      </span>
    </page-title>
    <router-view />
  </template>

  <template v-else>
    <page-title>Личные сообщения</page-title>
    <secondary-text class="login-required">
      Для доступа к личным сообщениям необходимо
      <router-link :to="{ name: 'home' }">войти</router-link>
      в систему
    </secondary-text>
  </template>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.unread-badge
  display: inline-block
  padding: 2px 8px
  margin-left: $small
  border-radius: $border-radius
  font-size: $secondary-font-size
  background-color: $button-bg
  color: $button-text

.login-required
  padding: $big
  text-align: center
  a
    color: $link
</style>
