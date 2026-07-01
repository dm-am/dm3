<script setup lang="ts">
import { computed, ref, onMounted, watch } from "vue";
import type { Username } from "@/shared/api/models/community";
// The public warnings/bans endpoints return bare `UserWarningsInfo` /
// `UserBanStatus` payloads (NOT a ListEnvelope). `moderationApi` already
// declares those exact shapes, so we consume it here — `communityApi`'s
// ListEnvelope-typed pair never matched the wire contract and always
// produced empty results.
import moderationApi, {
  BanType,
  type UserWarningsInfo,
  type UserBanStatus,
  type Ban,
} from "@/shared/api/moderationApi";
import { BlockTitle } from "@/shared/ui/Layout";
import { StatLine } from "@/shared/ui/StatLine";
import dayjs from "dayjs";

const WARNING_LIMIT = 6;

const props = withDefaults(
  defineProps<{
    username: Username;
    /**
     * Inline mode renders as a single "Нарушения: x/y" stat-line suitable
     * for embedding inside the identity info-stack (DM2 layout). Default
     * renders the full standalone "Нарушения" section with a BlockTitle and
     * additional context (current/last ban).
     */
    inline?: boolean;
  }>(),
  { inline: false },
);

const warningsInfo = ref<UserWarningsInfo | null>(null);
const banStatus = ref<UserBanStatus | null>(null);
const loading = ref(true);

async function fetchViolations() {
  loading.value = true;
  const [warningsResult, bansResult] = await Promise.all([
    moderationApi.getWarnings(props.username),
    moderationApi.getBans(props.username),
  ]);
  warningsInfo.value = warningsResult.data ?? null;
  banStatus.value = bansResult.data ?? null;
  loading.value = false;
}

onMounted(fetchViolations);
watch(() => props.username, fetchViolations);

const activeBan = computed(() => banStatus.value?.activeBan ?? null);
const history = computed(() => banStatus.value?.history ?? []);

const lastBan = computed(() => {
  if (!history.value.length) return null;
  // Newest first by start date (falls back to index order when absent).
  return [...history.value].sort((a, b) =>
    dayjs(b.startedUtc ?? 0).diff(dayjs(a.startedUtc ?? 0)),
  )[0];
});

const warningPoints = computed(() => warningsInfo.value?.totalPoints ?? 0);

const hasAnything = computed(
  () =>
    !!activeBan.value || history.value.length > 0 || warningPoints.value > 0,
);

function formatActiveBan(ban: Ban): string {
  if (ban.type === BanType.Permanent) return "полный бессрочный";
  if (ban.expiresUtc) return `до ${dayjs(ban.expiresUtc).format("DD.MM.YYYY")}`;
  return "активный";
}

function formatLastBan(ban: Ban, index: number): string {
  const ordinal = `${index + 1}-й`;
  const start = ban.startedUtc
    ? dayjs(ban.startedUtc).format("DD.MM.YYYY")
    : "";
  return `${ordinal}${start ? ` с ${start}` : ""}`;
}

const lastBanIndex = computed(() => {
  if (!lastBan.value) return -1;
  return history.value.indexOf(lastBan.value);
});
</script>

<template>
  <div v-if="inline && !loading" class="violations-inline">
    <StatLine
      label="Нарушения"
      :value="`${warningPoints}/${WARNING_LIMIT}`"
      :variant="activeBan || warningPoints > 0 ? 'negative' : 'default'"
    />
    <StatLine
      v-if="activeBan"
      label="Текущий бан"
      :value="formatActiveBan(activeBan)"
      variant="negative"
    />
    <StatLine
      v-if="lastBan && !activeBan"
      label="Последний бан"
      :value="formatLastBan(lastBan, lastBanIndex)"
    />
  </div>

  <section
    v-else-if="!inline && !loading && hasAnything"
    class="profile-violations"
  >
    <BlockTitle>Нарушения</BlockTitle>

    <StatLine
      v-if="activeBan"
      label="Текущий бан"
      :value="formatActiveBan(activeBan)"
      variant="negative"
    />
    <StatLine
      v-if="lastBan && !activeBan"
      label="Последний бан"
      :value="formatLastBan(lastBan, lastBanIndex)"
    />
    <StatLine
      label="Баллы предупреждений"
      :value="`${warningPoints}/${WARNING_LIMIT}`"
      :variant="warningPoints > 0 ? 'negative' : 'muted'"
    />
  </section>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Inputs"

.profile-violations
  // Flat — block title + StatLines. No red box wrappers.
</style>
