<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import { useUserStore } from "@/stores/user";
import accountApi from "@/api/requests/accountApi";
import devApi, { type TestAccountInfo } from "@/api/requests/devApi";
import { UserRole } from "@/api/models/community";
import BlockTitle from "@/components/layout/BlockTitle.vue";

const userStore = useUserStore();

const roles = [
  {
    value: UserRole.RegularUser,
    label: "RegularUser",
    description: "Рядовой пользователь",
  },
  { value: UserRole.Mentor, label: "Mentor", description: "Гоблин-наставник" },
  {
    value: UserRole.Moderator,
    label: "Moderator",
    description: "Младший гоблин",
  },
  {
    value: UserRole.SeniorModerator,
    label: "SeniorModerator",
    description: "Старший гоблин",
  },
  {
    value: UserRole.Admin,
    label: "Admin",
    description: "Тролль (администратор)",
  },
];

const accounts = ref<TestAccountInfo[]>([]);
const isLoading = ref(true);
const roleChangeMessage = ref("");

const currentUser = computed(() => userStore.user);
const isAuthenticated = computed(() => accountApi.isAuthenticated());

async function setRole(role: UserRole) {
  if (!isAuthenticated.value) {
    roleChangeMessage.value = "Необходимо авторизоваться";
    return;
  }

  roleChangeMessage.value = "";
  const { error } = await devApi.setRole(role);
  if (error) {
    roleChangeMessage.value = `Ошибка: ${error.title}`;
  } else {
    roleChangeMessage.value = `Роль изменена на ${role}. Перезагрузите страницу для применения.`;
    await userStore.fetchUser();
  }
}

function getRoleLabel(role: UserRole): string {
  const found = roles.find((r) => r.value === role);
  return found ? found.label : role;
}

onMounted(async () => {
  const { data } = await devApi.getAllUsers();
  if (data) {
    accounts.value = data;
  }
  isLoading.value = false;
});
</script>

<template>
  <div class="dev-accounts-page">
    <h1 class="page-title">Dev: Аккаунты и роли</h1>

    <!-- Role Switcher -->
    <section class="dev-section">
      <BlockTitle>Смена роли</BlockTitle>

      <div v-if="!isAuthenticated" class="not-authenticated">
        <p>Авторизуйтесь, чтобы менять роль</p>
      </div>

      <div v-else class="role-switcher">
        <p class="current-role">
          Текущий пользователь: <strong>{{ currentUser?.login }}</strong>
          <span v-if="currentUser?.roles?.length"
            >({{ currentUser.roles.join(", ") }})</span
          >
        </p>

        <div class="roles-grid">
          <button
            v-for="role in roles"
            :key="role.value"
            class="role-btn"
            :class="{ active: currentUser?.roles?.includes(role.value) }"
            @click="setRole(role.value)"
          >
            <span class="role-name">{{ role.label }}</span>
            <span class="role-desc">{{ role.description }}</span>
          </button>
        </div>

        <p v-if="roleChangeMessage" class="message">{{ roleChangeMessage }}</p>
      </div>
    </section>

    <!-- All Accounts -->
    <section class="dev-section">
      <BlockTitle>Все аккаунты</BlockTitle>

      <table v-if="!isLoading" class="accounts-table">
        <thead>
          <tr>
            <th>Имя пользователя</th>
            <th>Пароль</th>
            <th>Роль</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="account in accounts" :key="account.login">
            <td>
              <code>{{ account.login }}</code>
            </td>
            <td>
              <code>{{ account.password }}</code>
            </td>
            <td>{{ getRoleLabel(account.role) }}</td>
          </tr>
        </tbody>
      </table>
    </section>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"
@import "src/assets/styles/Tables"
@import "src/assets/styles/Inputs"

.dev-accounts-page
  max-width: 800px

.page-title
  color: $heading
  font-size: $title-font-size
  font-weight: bold
  text-transform: uppercase
  letter-spacing: 0.5px
  margin: $medium 0

.dev-section
  margin: $big 0

.not-authenticated
  padding: $medium
  background-color: $bg-highlight-yellow
  color: $text

  p
    margin: 0

.role-switcher
  .current-role
    margin: 0 0 $medium
    color: $text

    strong
      color: $link

.roles-grid
  display: grid
  grid-template-columns: repeat(auto-fill, minmax(180px, 1fr))
  gap: $small

.role-btn
  +button
  display: flex
  flex-direction: column
  align-items: flex-start
  gap: $tiny
  padding: $small $medium
  text-align: left
  width: 100%

  &.active
    background-color: $bg-highlight-green
    border-color: $accent-green

.role-name
  font-weight: bold

.role-desc
  font-size: $tertiary-font-size
  color: $text-muted
  font-weight: normal

.message
  margin: $small 0 0
  padding: $small
  background-color: $bg-element-accent
  color: $text

.accounts-table
  width: 100%
  +table

  th, td
    padding: $small $medium
    text-align: left

  th
    +table-header

  tr
    +table-row

  code
    font-family: $code-font
    color: $link
</style>
