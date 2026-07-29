<template>
  <SidebarBlock token="SupportUs">
    <template #title>Поддержка проекта</template>
    <SidebarSkeleton v-if="loading && !fundraising" :lines="3" />
    <template v-else>
      <ProgressBar
        v-if="fundraising"
        :current="fundraising.collectedAmount"
        :goal="fundraising.goalAmount"
      >
        {{ fundraising.collectedAmount.toLocaleString("ru-RU") }} /
        {{ fundraising.goalAmount.toLocaleString("ru-RU") }} р.
      </ProgressBar>
      <SecondaryText v-else-if="error" class="fetch-error">
        Не удалось загрузить сбор.
        <button type="button" class="retry-link" @click="fetch(true)">
          Повторить
        </button>
      </SecondaryText>
      Dungeon Master &ndash; некоммерческий проект.<br />
      <router-link :to="{ name: 'support' }">Помогите нам</router-link> хотя бы
      не испортить его!
      <div v-if="isAdmin" class="admin-edit">
        <router-link
          class="admin-edit-link"
          :to="{ name: 'moderation-fundraising' }"
        >
          <SvgIcon name="pencil" class="admin-edit-icon" />Изменить сбор
        </router-link>
      </div>
    </template>
  </SidebarBlock>
</template>

<script setup lang="ts">
import { computed } from "vue";
import ProgressBar from "@/shared/ui/ProgressBar/ProgressBar.vue";
import { SvgIcon } from "@/shared/ui/Icon";
import SidebarBlock from "./SidebarBlock.vue";
import SidebarSkeleton from "./SidebarSkeleton.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import fundraisingApi, { type Fundraising } from "@/shared/api/fundraisingApi";
import { useApiResource } from "@/shared/lib/composables/useApiResource";
import { useAuthStore, userIsAdmin } from "@/entities/user";
import type { Envelope } from "@/shared/api/models/common";

const userStore = useAuthStore();

// Admin-only edit affordance: guests and regular users never see it,
// so the guest visual stays unchanged.
const isAdmin = computed(() => userIsAdmin(userStore.user));

const { data, loading, error, fetch } = useApiResource<Envelope<Fundraising>>(
  () => fundraisingApi.getFundraising(),
  { cacheMs: 300_000 },
);

const fundraising = computed(() => data.value?.resource ?? null);

// Fetch synchronously during setup so `loading` is already true on the
// first render (avoids a flash of the no-progress-bar layout). On error
// the block renders an inline retry line instead of the progress bar,
// while the CTA text below still renders.
void fetch();
</script>

<style scoped lang="sass">
@import "@/assets/styles/Inputs"

.fetch-error
  display: block
  margin-bottom: $small

.retry-link
  +inline-link-button

.admin-edit
  margin-top: $small

.admin-edit-link
  display: inline-flex
  align-items: center
  gap: $minor
  font-size: $secondary-font-size
  color: $text-muted

  &:hover
    color: $link-hover

.admin-edit-icon
  width: 14px
  height: 14px
</style>
