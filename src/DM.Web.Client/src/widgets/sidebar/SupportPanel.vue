<template>
  <SidebarBlock token="SupportUs" title="Поддержка проекта">
    <SidebarSkeleton v-if="loading && !fundraising" :lines="3" />
    <template v-else>
      <li v-if="fundraising">
        <!-- What the money is for, above the bar -->
        <div class="goal-title">{{ fundraising.title }}</div>
        <ProgressBar
          :current="fundraising.collectedAmount"
          :goal="fundraising.goalAmount"
        >
          {{ fundraising.collectedAmount.toLocaleString("ru-RU") }} /
          {{ fundraising.goalAmount.toLocaleString("ru-RU") }} р.
        </ProgressBar>
      </li>
      <li v-else-if="error">
        <SecondaryText class="fetch-error">
          Не удалось загрузить сбор.
          <button type="button" class="retry-link" @click="fetch(true)">
            Повторить
          </button>
        </SecondaryText>
      </li>
      <li>
        Dungeon Master &ndash; некоммерческий проект.<br />
        <router-link :to="{ name: 'donate' }">Помогите нам</router-link> хотя бы
        не испортить его!
      </li>
      <li v-if="isAdmin" class="admin-edit">
        <router-link
          class="admin-edit-link"
          :to="{ name: 'moderation-fundraising' }"
        >
          <SvgIcon name="pencil" class="admin-edit-icon" />Изменить сбор
        </router-link>
      </li>
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
import { fundraisingApi, type Fundraising } from "@/entities/fundraising";
import { useApiResource } from "@/shared/lib/composables/useApiResource";
import { useAuthStore, userIsAdmin } from "@/entities/user";
import type { Envelope } from "@/shared/api/models/common";

const userStore = useAuthStore();

const isAdmin = computed(() => userIsAdmin(userStore.user));

const { data, loading, error, fetch } = useApiResource<Envelope<Fundraising>>(
  () => fundraisingApi.getFundraising(),
  { cacheMs: 300_000 },
);

const fundraising = computed(() => data.value?.resource ?? null);

// In setup, so loading is already true on the first render.
void fetch();
</script>

<style scoped lang="sass">
@use "@/assets/styles/Links" as *

.goal-title
  font-weight: bold

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
  +muted-link

.admin-edit-icon
  width: 14px
  height: 14px
</style>
