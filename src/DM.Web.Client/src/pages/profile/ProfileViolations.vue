<script setup lang="ts">
import { formatDate } from "@/shared/lib/utils/datetime";
import { computed, ref, watch } from "vue";
import type { Username } from "@/shared/api/models/community";
// The public warnings and bans endpoints answer bare payloads, not a ListEnvelope.
import {
  warningApi,
  banApi,
  BanType,
  type UserWarningsInfo,
  type PublicUserBanStatus,
  type PublicBan,
} from "@/entities/moderation";
import { StatLine } from "@/shared/ui/StatLine";
import {
  shownOnPage,
  useGuardedRequest,
} from "@/shared/lib/composables/useGuardedRequest";

// The scale the points are drawn against (Violator.WarningPointsScale, compared by
// ClientFieldLimitsShould); it triggers nothing.
const WARNING_LIMIT = 6;

const props = defineProps<{
  username: Username;
}>();

const warningsInfo = ref<UserWarningsInfo | null>(null);
const banStatus = ref<PublicUserBanStatus | null>(null);

// The profile can change under the line: the guard drops the older pair of answers.
const { loading, error, run } = useGuardedRequest({
  message: "не удалось загрузить",
  clearErrorOnStart: true,
});

function fetchViolations() {
  return run(
    async () => {
      // Taken over as they land: the line says a failure itself.
      const [warnings, bans] = await Promise.all([
        warningApi.getWarnings(props.username).then(shownOnPage),
        banApi.getBans(props.username).then(shownOnPage),
      ]);
      // One line on screen, so one failure is the line's; the partial data is still handed over.
      return {
        data: { warnings: warnings.data, bans: bans.data },
        error: warnings.error ?? bans.error,
      };
    },
    (data) => {
      warningsInfo.value = data?.warnings ?? null;
      banStatus.value = data?.bans ?? null;
    },
  );
}

// In setup, not on mount: `run` raises `loading` before its first await, so
// the first paint does not draw 0/6 for warnings not yet read.
fetchViolations();
watch(() => props.username, fetchViolations);

const activeBan = computed(() => banStatus.value?.activeBan ?? null);
const history = computed(() => banStatus.value?.history ?? []);

// Newest first, so the newest ban's ordinal is the count of bans.
const lastBan = computed(() => history.value[0] ?? null);
const lastBanOrdinal = computed(() => history.value.length);

const warningPoints = computed(() => warningsInfo.value?.totalPoints ?? 0);

/**
 * The warnings behind the points, in the log narrowed to this user, when there
 * are any: a verbal warning carries no points, so the count decides.
 */
const warningsLogLink = computed(() =>
  (warningsInfo.value?.activeCount ?? 0) > 0
    ? { name: "warnings", query: { username: props.username } }
    : undefined,
);

function formatActiveBan(ban: PublicBan): string {
  if (ban.type === BanType.Permanent) return "полный бессрочный";
  if (ban.expiresUtc) return `до ${formatDate(ban.expiresUtc)}`;
  return "активный";
}

function formatLastBan(ban: PublicBan, ordinal: number): string {
  const start = ban.startedUtc ? formatDate(ban.startedUtc) : "";
  return `${ordinal}-й${start ? ` с ${start}` : ""}`;
}
</script>

<template>
  <div class="violations-inline">
    <StatLine
      v-if="!loading && !error"
      label="Нарушения"
      :value="`${warningPoints}/${WARNING_LIMIT}`"
      :variant="activeBan || warningPoints > 0 ? 'negative' : 'default'"
      :to="warningsLogLink"
    />
    <StatLine
      v-else-if="error"
      label="Нарушения"
      value="не удалось загрузить"
      variant="muted"
    />
    <StatLine
      v-if="!loading && activeBan"
      label="Текущий бан"
      :value="formatActiveBan(activeBan)"
      variant="negative"
    />
    <StatLine
      v-if="!loading && lastBan && !activeBan"
      label="Последний бан"
      :value="formatLastBan(lastBan, lastBanOrdinal)"
    />
  </div>
</template>

<style scoped lang="sass">

// The height of one line, so the column does not jump when the line loads.
.violations-inline
  min-height: calc($font-size * 1.25)
</style>
