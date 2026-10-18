<script setup lang="ts">
/**
 * RulesStaffTable - Staff/administration table
 *
 * Displays site administration with their roles and responsibilities, on the
 * list the rules page draws its other tables with (ExpandableList): a plain
 * table of three columns at every width, its rows plain rows that hold links.
 */

import { computed, onMounted, ref } from "vue";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import {
  ExpandableList,
  type ExpandableListColumn,
} from "@/shared/ui/ExpandableList";
import { UserLink } from "@/entities/user";
import { UserRole, type User } from "@/shared/api/models/community";
import { userApi } from "@/entities/user";
import { ROLE_INFO, STAFF_ROLES } from "@/shared/config/roles";
import { ADMIN_LINKS } from "@/shared/config/helpLinks";
import { VALUE_UNAVAILABLE } from "@/shared/lib/constants/copy";

interface RoleGroup {
  role: UserRole;
  users: User[];
}

// Type alias (not interface) so the implicit index signature satisfies the
// ExpandableItem constraint of ExpandableList.
type StaffRow = {
  id: UserRole;
  title: string;
  nickname: string;
  description: string;
  users: User[];
};

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
    STAFF_ROLES.map((role) => userApi.getUsersByRole(role)),
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

const rows = computed<StaffRow[]>(() =>
  roleGroups.value.map(({ role, users }) => ({
    id: role,
    title: ROLE_INFO[role].title,
    nickname: ROLE_INFO[role].nickname,
    description: ROLE_INFO[role].description,
    users,
  })),
);

// Every column holds its longest word ("Администраторы" in bold, a login
// whole), and the room over that is shared (ExpandableList).
const columns: ExpandableListColumn<StaffRow>[] = [
  { key: "title", label: "Роль" },
  { key: "description", label: "Функции" },
  { key: "users", label: "Состав" },
];
</script>

<template>
  <section id="admins" class="admin-section">
    <BlockTitle>Администрация</BlockTitle>

    <ExpandableList
      :items="rows"
      :columns="columns"
      :allow-multiple="true"
      row-align="baseline"
      plain-rows
    >
      <template #cell-title="{ item }">
        <router-link
          :to="{ name: 'community', query: { role: item.id } }"
          class="role-title"
        >
          {{ item.title }}
        </router-link>
        <span v-if="item.nickname" class="role-nickname">
          ({{ item.nickname }})
        </span>
      </template>

      <template #cell-description="{ item }">
        <span class="role-functions">{{ item.description }}</span>
      </template>

      <template #cell-users="{ item }">
        <!-- One shimmer line per cell while the batched request is in flight -->
        <span v-if="loading" class="skeleton-line" aria-hidden="true" />
        <span v-else-if="error" class="load-error"
          >Не удалось загрузить.
          <button type="button" class="retry-link" @click="loadRoleGroups">
            Повторить
          </button></span
        >
        <template v-else-if="item.users.length">
          <span
            v-for="user in item.users"
            :key="user.username"
            class="user-item"
          >
            <UserLink :user="user" hide-badge />
          </span>
        </template>
        <span v-else class="no-users">{{ VALUE_UNAVAILABLE }}</span>
      </template>
    </ExpandableList>

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
@use "@/assets/styles/Inputs" as *
@use "@/assets/styles/Skeleton" as *

// The last block of the rules page: the space above it is the step between
// the blocks of the page, and none goes under it. The page ends where it ends,
// and what stands under the page is the shell's to space; a margin here stood
// on top of that, and the rules ended 80 above it.
.admin-section
  margin-top: $big

// Color and hover come from the global `a` rule (Reset.sass); only the
// weight is local, the site's one bold.
.role-title
  font-weight: 700

// Two visual lines inside ONE cell, so the break belongs to the nickname and
// not to a layout on the cell: a cell that laid its children out would be the
// flex box this table left. 2px is the former gap.
.role-nickname
  display: block
  margin-top: 2px
  color: $text-muted
  font-size: $secondary-font-size

.role-functions
  color: $text
  font-size: $secondary-font-size
  line-height: 1.4

// One name per line inside the cell, the same way: the line break is on the
// item, the cell only holds the column width.
.user-item
  display: block
  line-height: 1.4

  & + &
    margin-top: 2px

.no-users
  color: $text-muted

// One shimmer line per cell while the batched role request is loading
.skeleton-line
  display: block
  width: 90px
  max-width: 100%
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
</style>
