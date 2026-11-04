<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import {
  ExpandableList,
  type ExpandableListColumn,
} from "@/shared/ui/ExpandableList";
import { ErrorState } from "@/shared/ui/ErrorState";
import { UserLink, userApi } from "@/entities/user";
import { UserRole, type User } from "@/shared/api/models/community";
import { ROLE_INFO, STAFF_ROLES } from "@/shared/config/roles";
import { ADMIN_LINKS } from "@/shared/config/helpLinks";
import { VALUE_UNAVAILABLE } from "@/shared/lib/constants/copy";
import { describeFailureOrWait } from "@/shared/lib/errors";

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
const failure = ref<string | null>(null);

const LOAD_FAILURE = "Не удалось загрузить состав администрации";

// One loading state and one error state for the four requests, not one per row.
async function loadRoleGroups() {
  loading.value = true;
  failure.value = null;
  const results = await Promise.all(
    STAFF_ROLES.map((role) => userApi.getUsersByRole(role)),
  );
  loading.value = false;
  const failed = results.find(({ error }) => error)?.error;
  if (failed) {
    failure.value = describeFailureOrWait(failed, LOAD_FAILURE);
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

// "Роль" and "Состав" are as wide as what they hold, a role name or a login on
// one line, and "Функции" takes the rest (ExpandableList's fit). Without its
// members the table keeps the roles and loses the column; the error under it
// says why.
const columns = computed(() => {
  const shown: ExpandableListColumn<StaffRow>[] = [
    { key: "title", label: "Роль", fit: true },
    { key: "description", label: "Функции" },
  ];
  if (!failure.value) shown.push({ key: "users", label: "Состав", fit: true });
  return shown;
});
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

    <ErrorState
      v-if="failure"
      class="staff-error"
      :message="failure"
      :retry="loadRoleGroups"
    />

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
@use "@/assets/styles/Links" as *
@use "@/assets/styles/Skeleton" as *

// The last block of the page: what stands under the page is the shell's to space.
.admin-section
  margin-top: $big

// Color and hover come from the global `a` rule (Reset.sass); only the
// weight is local, the site's one bold.
.role-title
  font-weight: 700

// Two lines inside one cell: the break belongs to the nickname, not to a layout of the cell.
.role-nickname
  display: block
  margin-top: $tiny
  color: $text-muted
  font-size: $secondary-font-size

.role-functions
  color: $text
  font-size: $secondary-font-size
  line-height: 1.4

// One name per line, the same way: the break is on the item.
.user-item
  display: block
  line-height: 1.4

  & + &
    margin-top: $tiny

.no-users
  color: $text-muted

// One shimmer line per cell while the batched role request is loading: a login's length, a small
// line's height.
.skeleton-line
  display: block
  width: 3 * $big
  max-width: 100%
  height: $secondary-font-size
  +skeleton-shimmer

// $small: the error belongs to the table above it, as the links under it do.
.staff-error
  margin-top: $small

// $small: the muted caption belongs to the table above it, as on the forum pages.
.useful-links
  margin-top: $small
  +muted-links-line

  .links-label
    font-weight: 400

  a
    +muted-link
</style>
