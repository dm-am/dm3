<script setup lang="ts">
import ThePaging from "@/components/ThePaging.vue";
import { storeToRefs } from "pinia";
import UserRating from "@/components/community/UserRating.vue";
import UserOnline from "@/components/community/UserOnline.vue";
import { useCommunityStore } from "@/stores/community";
import { useRoute } from "vue-router";

const { users, usersError } = storeToRefs(useCommunityStore());
const route = useRoute();
</script>

<template>
  <secondary-text v-if="usersError === 403" class="users-list-error">
    Недостаточно прав для просмотра неактивированных пользователей
  </secondary-text>

  <template v-else>
    <the-paging
      v-if="users"
      :paging="users.paging!"
      :to="{ name: route.name, params: route.params }"
    />

  <div class="users-list-table">
    <div class="users-list-header">
      <div>#</div>
      <div>Имя пользователя</div>
      <div>Рейтинг</div>
      <div>В сети</div>
      <div>Имя</div>
      <div>Местоположение</div>
    </div>

    <secondary-text v-if="users && !users.resources.length" class="users-list-none"
      >Нет пользователей</secondary-text
    >
    <template v-else-if="users">
      <div
        class="users-list-row"
        v-for="(user, number) in users.resources"
        :key="user.login"
      >
        <span class="number">{{
          number + users.paging!.size * (users.paging!.current - 1) + 1
        }}</span>
      <user-link :user="user" />
      <user-rating :user="user" />
      <user-online :user="user" :detailed="true" />
      <span>{{ user.name }}</span>
      <span>{{ user.location }}</span>
      </div>
    </template>
  </div>
  </template>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Tables"

$grid-template: [number] 6% [login] auto [rating] 12% [online] 8% [name] 25% [location] 25%

.users-list-table
  +table

.users-list-header
  display: grid
  grid-template-columns: $grid-template
  +table-header
  +table-columns

.users-list-row
  display: grid
  grid-template-columns: $grid-template
  +table-row
  +table-columns

.users-list-none
  margin: $medium 0
  text-align: center

.users-list-error
  margin: $big 0
  text-align: center
</style>
