<script setup lang="ts">
/**
 * RulesStaffTable - Staff/administration table
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
}

const roleGroups = ref<RoleGroup[]>(
  STAFF_ROLES.map((role) => ({ role, users: [] })),
);
const loading = ref(true);
const error = ref(false);

// All four role requests are batched: one loading state, one error state
// (with a single inline retry) instead of per-row spinners/errors.
async function loadRoleGroups() {
  loading.value = true;
  error.value = false;
  const results = await Promise.all(
    STAFF_ROLES.map((role) => communityApi.getUsersByRole(role)),
  );
  loading.value = false;
  if (results.some(({ error: reqError }) => reqError)) {
    error.value = true;
    return;
  }
  roleGroups.value = STAFF_ROLES.map((role, index) => ({
    role,
    users: results[index].data?.resources ?? [],
  }));
}

onMounted(loadRoleGroups);
</script>

<template>
  <section id="admins" class="admin-section">
    <BlockTitle>Администрация</BlockTitle>

    <div class="admin-table">
      <div class="admin-header">
        <span class="role-col">Роль</span>
        <span class="desc-col">Функции</span>
        <span class="users-col">Состав</span>
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
          <!-- One shimmer line per cell while the batched request is in flight -->
          <span v-if="loading" class="skeleton-line" aria-hidden="true" />
          <span v-else-if="error" class="load-error"
            >Не удалось загрузить.
            <button type="button" class="retry-link" @click="loadRoleGroups">
              Повторить
            </button></span
          >
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

    <p class="useful-links">
      <span class="links-label">Полезные ссылки:</span>{{ " "
      }}<template v-for="(link, idx) in ADMIN_LINKS" :key="link.title"
        ><a
          v-if="link.external"
          :href="link.url"
          target="_blank"
          rel="noopener noreferrer"
          >{{ link.title }}</a
        ><router-link v-else :to="link.url">{{ link.title }}</router-link
        ><template v-if="idx < ADMIN_LINKS.length - 1">, </template></template
      >
    </p>
  </section>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Tables"
@import "src/assets/styles/Inputs"
@import "src/assets/styles/Skeleton"

.admin-section
  margin: $big 0

.admin-table
  +table

// CSS table, not grid: grid items copy with newlines between cells (the
// one-line header copied as "Роль\nФункции\nСостав"), table cells copy
// tab-separated like the site's real data tables. Fixed layout with the
// same explicit column widths keeps the cells aligned with the grid rows
// below (180px / auto / 220px, no gap in either layout).
.admin-header
  display: table
  width: 100%
  box-sizing: border-box
  table-layout: fixed
  +table-header

  > span
    display: table-cell

  > .role-col
    width: 180px

  > .users-col
    width: 220px

.admin-row
  display: grid
  grid-template-columns: 180px 1fr 220px
  align-items: start
  +table-row

.role-col
  display: flex
  flex-direction: column
  gap: 2px

// Color and hover come from the global `a` rule (Reset.sass); only the
// weight is local.
.role-title
  font-weight: 600

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

// One shimmer line per cell while the batched role request is loading
.skeleton-line
  display: block
  width: 90px
  height: 14px
  +skeleton-shimmer

.load-error
  color: $text-muted
  font-size: $secondary-font-size

// font-size overrides the mixin's "font: inherit" (the shorthand resets
// size), so it must stay after the include; the "&" block keeps the CSS
// cascade order explicit (avoids the Sass mixed-decls deprecation).
.retry-link
  +inline-link-button
  &
    font-size: $secondary-font-size

// $small, not $medium: the muted caption belongs to the table above it,
// so it sits close (same table-to-caption distance as the forum pages).
.useful-links
  margin-top: $small
  +muted-links-line

  .links-label
    font-weight: 500

  a
    +muted-link

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
