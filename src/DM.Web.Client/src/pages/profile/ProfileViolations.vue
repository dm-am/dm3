<script setup lang="ts">
import { ref, onMounted, watch } from "vue";
import type { Username, PublicWarning, PublicBan } from "@/shared/api/models/community";
import { communityApi } from "@/shared/api";
import dayjs from "dayjs";

const props = defineProps<{
  username: Username;
}>();

const warnings = ref<PublicWarning[]>([]);
const bans = ref<PublicBan[]>([]);
const loading = ref(true);

async function fetchViolations() {
  loading.value = true;
  const [warningsResult, bansResult] = await Promise.all([
    communityApi.getWarnings(props.username),
    communityApi.getBans(props.username),
  ]);

  warnings.value = warningsResult.data?.resources || [];
  bans.value = bansResult.data?.resources || [];
  loading.value = false;
}

onMounted(fetchViolations);
watch(() => props.username, fetchViolations);

const activeBans = ref<PublicBan[]>([]);
const hasViolations = ref(false);

watch([warnings, bans], () => {
  activeBans.value = bans.value.filter((b) => b.isActive);
  hasViolations.value = warnings.value.length > 0 || activeBans.value.length > 0;
});
</script>

<template>
  <section v-if="hasViolations && !loading" class="profile-violations">
    <h3 class="section-title">Нарушения</h3>

    <div v-if="activeBans.length" class="violations-group">
      <div v-for="ban in activeBans" :key="ban.id" class="violation ban">
        <div class="violation-header">
          <span class="violation-type">Бан</span>
          <span class="violation-date">
            {{ ban.isPermanent ? "Постоянный" : `до ${dayjs(ban.endUtc).format("DD.MM.YYYY")}` }}
          </span>
        </div>
        <div class="violation-reason">{{ ban.reason }}</div>
        <div class="violation-moderator">Модератор: {{ ban.moderatorUsername }}</div>
      </div>
    </div>

    <div v-if="warnings.length" class="violations-group">
      <div v-for="warning in warnings" :key="warning.id" class="violation warning">
        <div class="violation-header">
          <span class="violation-type">Предупреждение ({{ warning.points }} балл.)</span>
          <span v-if="warning.expiresUtc" class="violation-date">
            до {{ dayjs(warning.expiresUtc).format("DD.MM.YYYY") }}
          </span>
        </div>
        <div class="violation-reason">{{ warning.text }}</div>
        <div class="violation-moderator">Модератор: {{ warning.moderatorUsername }}</div>
      </div>
    </div>
  </section>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.profile-violations
  background: rgba($accent-red, 0.1)
  border: 1px solid rgba($accent-red, 0.3)
  border-radius: $border-radius
  padding: $medium
  margin-bottom: $medium

.section-title
  color: $accent-red
  margin: 0 0 $small
  font-size: 1rem

.violations-group
  display: flex
  flex-direction: column
  gap: $small

.violation
  background: $bg-element
  border-radius: $border-radius
  padding: $small

.ban
  border-left: 3px solid $accent-red

.warning
  border-left: 3px solid $heading

.violation-header
  display: flex
  justify-content: space-between
  margin-bottom: $tiny

.violation-type
  font-weight: bold
  color: $text

.violation-date
  font-size: $secondary-font-size
  color: $text-muted

.violation-reason
  color: $text
  margin-bottom: $tiny

.violation-moderator
  font-size: $secondary-font-size
  color: $text-meta
</style>
