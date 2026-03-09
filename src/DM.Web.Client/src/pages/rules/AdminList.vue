<script setup lang="ts">
/**
 * AdminList - Таблица администрации
 * Классический табличный формат с колонками
 */

import { ref, onMounted } from "vue";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import { UserLink } from "@/entities/user";
import { UserRole, type User } from "@/shared/api/models/community";
import { communityApi } from "@/shared/api";
import { ROLE_INFO, STAFF_ROLES } from "@/shared/config/roles";
import { ADMIN_LINKS } from "@/shared/config/helpLinks";

interface RoleGroup {
  role: UserRole;
  users: User[];
  loading: boolean;
}

const roleGroups = ref<RoleGroup[]>(
  STAFF_ROLES.map((role) => ({
    role,
    users: [],
    loading: true,
  })),
);

async function loadUsersByRole(group: RoleGroup) {
  const { data, error } = await communityApi.getUsersByRole(group.role);
  group.loading = false;
  if (!error && data) {
    group.users = data.resources;
  }
}

onMounted(() => {
  roleGroups.value.forEach(loadUsersByRole);
});
</script>

<template>
  <section class="admin-section">
    <BlockTitle>Администрация</BlockTitle>

    <div class="admin-table">
      <div class="admin-header">
        <span>Роль</span>
        <span>Функции</span>
        <span></span>
      </div>
      <div v-for="group in roleGroups" :key="group.role" class="admin-row">
        <span class="role-cell">
          <span class="role-title">{{ ROLE_INFO[group.role].title }}</span>
          <span v-if="ROLE_INFO[group.role].nickname" class="role-nickname">
            ({{ ROLE_INFO[group.role].nickname }})
          </span>
        </span>
        <span class="desc-cell">{{ ROLE_INFO[group.role].description }}</span>
        <span class="users-cell">
          <template v-if="group.loading">...</template>
          <template v-else-if="group.users.length">
            <div
              v-for="user in group.users"
              :key="user.username"
              class="user-item"
            >
              <UserLink :user="user" />
            </div>
          </template>
          <span v-else class="no-users">—</span>
        </span>
      </div>
    </div>

    <p class="useful-links">
      <span class="links-label">Полезные ссылки:</span>
      <template v-for="(link, idx) in ADMIN_LINKS" :key="link.title">
        <a :href="link.url" target="_blank" rel="noopener noreferrer">{{
          link.title
        }}</a
        ><template v-if="idx < ADMIN_LINKS.length - 1">, </template
        ><template v-else>.</template>
      </template>
    </p>
  </section>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"
@import "src/assets/styles/Tables"

.admin-section
  margin: $big 0

.admin-table
  +table

.admin-header
  display: grid
  grid-template-columns: 180px 1fr 220px
  padding: $small $medium
  +table-header

  span
    padding: 0

.admin-row
  display: grid
  grid-template-columns: 180px 1fr 220px
  padding: $small $medium
  align-items: start
  +table-row

  span
    padding: $tiny 0

.role-cell
  display: flex
  flex-direction: column
  gap: 2px

.role-title
  font-weight: 600

.role-nickname
  color: $text-muted
  font-size: $secondary-font-size

.desc-cell
  color: $text
  font-size: $secondary-font-size
  line-height: 1.4

.users-cell
  display: flex
  flex-direction: column
  gap: 2px

.user-item
  line-height: 1.4

.no-users
  color: $text-muted

.useful-links
  margin-top: $medium
  font-size: $secondary-font-size
  color: $text-muted

  .links-label
    font-weight: 500
    margin-right: $small

  a
    color: $text-muted
    text-decoration: none
    transition: color 0.15s ease
    &:hover
      color: $link
      text-decoration: underline
    &:focus-visible
      outline: 2px solid $link
      outline-offset: 2px

@media (max-width: 768px)
  .admin-header
    display: none

  .admin-row
    display: flex
    flex-direction: column
    gap: $tiny

  .role-cell
    flex-direction: row
    align-items: baseline
    gap: $tiny

  .desc-cell
    color: $text-muted

  .users-cell
    flex-direction: row
    flex-wrap: wrap
    gap: $small
</style>
