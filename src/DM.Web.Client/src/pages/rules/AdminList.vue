<script setup lang="ts">
/**
 * AdminList - Staff/administration table
 *
 * Displays site administration with their roles and responsibilities.
 * Uses unified table styling from _Tables.sass.
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
        <span class="role-col">Роль</span>
        <span class="desc-col">Функции</span>
        <span class="users-col"></span>
      </div>
      <div v-for="group in roleGroups" :key="group.role" class="admin-row">
        <span class="role-col">
          <router-link
            :to="{ name: 'community', query: { role: group.role } }"
            class="role-title"
          >
            {{ ROLE_INFO[group.role].title }}
          </router-link>
          <span v-if="ROLE_INFO[group.role].nickname" class="role-nickname">
            ({{ ROLE_INFO[group.role].nickname }})
          </span>
        </span>
        <span class="desc-col">{{ ROLE_INFO[group.role].description }}</span>
        <span class="users-col">
          <template v-if="group.loading">...</template>
          <template v-else-if="group.users.length">
            <span
              v-for="user in group.users"
              :key="user.username"
              class="user-item"
            >
              <UserLink :user="user" hide-badge />
            </span>
          </template>
          <span v-else class="no-users">—</span>
        </span>
      </div>
    </div>

    <p class="useful-links"><span class="links-label">Полезные ссылки:</span>{{ " " }}<template v-for="(link, idx) in ADMIN_LINKS" :key="link.title"><a v-if="link.external" :href="link.url" target="_blank" rel="noopener noreferrer">{{ link.title }}</a><router-link v-else :to="link.url">{{ link.title }}</router-link><template v-if="idx < ADMIN_LINKS.length - 1">, </template></template></p>
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
  +table-header

.admin-row
  display: grid
  grid-template-columns: 180px 1fr 220px
  align-items: start
  +table-row

.role-col
  display: flex
  flex-direction: column
  gap: 2px

.role-title
  font-weight: 600
  color: $link
  text-decoration: none

  &:hover
    color: $link-hover
    text-decoration: underline

.role-nickname
  color: $text-muted
  font-size: $secondary-font-size

.desc-col
  color: $text
  font-size: $secondary-font-size
  line-height: 1.4

.users-col
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

  a
    color: $text-muted
    text-decoration: none
    &:hover
      color: $link
      text-decoration: underline
    &:focus-visible
      outline: 2px solid $link
      outline-offset: 2px

@media (max-width: $mobile-breakpoint)
  .admin-header
    display: none

  .admin-row
    display: flex
    flex-direction: column
    gap: $tiny

  .role-col
    flex-direction: row
    align-items: baseline
    gap: $tiny

  .desc-col
    color: $text-muted

  .users-col
    flex-direction: row
    flex-wrap: wrap
    gap: $small
</style>
