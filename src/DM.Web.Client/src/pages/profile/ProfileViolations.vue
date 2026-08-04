<script setup lang="ts">
import { formatDate } from "@/shared/lib/utils/datetime";
import { computed, ref, watch } from "vue";
import type { Username } from "@/shared/api/models/community";
// The public warnings/bans endpoints return bare `UserWarningsInfo` /
// `PublicUserBanStatus` payloads (NOT a ListEnvelope). `moderationApi`
// already declares those exact trimmed-public shapes, so we consume it
// here — the old ListEnvelope-typed pair on the user client never matched the
// contract and always produced empty results.
import {
  moderationApi,
  BanType,
  type UserWarningsInfo,
  type PublicUserBanStatus,
  type PublicBan,
} from "@/entities/moderation";
import { StatLine } from "@/shared/ui/StatLine";
import { useGuardedRequest } from "@/shared/lib/composables";

// Mirrors the backend warning policy (6+ points in 30 days triggers an
// automatic ban — see WarningController.cs remarks). Not exposed by the
// public API, so it stays a client-side constant kept in sync by hand.
const WARNING_LIMIT = 6;

const props = defineProps<{
  username: Username;
}>();

const warningsInfo = ref<UserWarningsInfo | null>(null);
const banStatus = ref<PublicUserBanStatus | null>(null);

// The line refetches when the profile changes under it, so two pairs of answers
// can be on the wire at once; the guard drops the older pair instead of letting
// whichever landed last decide whose violations are on screen.
const { loading, error, run } = useGuardedRequest({
  message: "не удалось загрузить",
  clearErrorOnStart: true,
});

function fetchViolations() {
  return run(
    async () => {
      const [warnings, bans] = await Promise.all([
        moderationApi.getWarnings(props.username),
        moderationApi.getBans(props.username),
      ]);
      // Both halves are one line on screen, so one failure is the line's
      // failure — and the partial data is still handed over, as it was.
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

// Called during setup rather than on mount: `run` raises `loading` before its
// first await, so the first paint already has the flag the template reads. On
// mount the flag would be false for one frame and the line would draw 0/6 for a
// profile whose warnings have not been read yet.
fetchViolations();
watch(() => props.username, fetchViolations);

const activeBan = computed(() => banStatus.value?.activeBan ?? null);
const history = computed(() => banStatus.value?.history ?? []);

// API returns history newest-first, so the newest ban IS the last one
// chronologically — its ordinal is the total ban count, not always "1-й".
const lastBan = computed(() => history.value[0] ?? null);
const lastBanOrdinal = computed(() => history.value.length);

const warningPoints = computed(() => warningsInfo.value?.totalPoints ?? 0);

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
@import "@/assets/styles/Inputs"

// Reserve height for the inline "Нарушения" line while it loads, so the
// identity column doesn't jump once the fetch resolves (one StatLine ==
// one line-height 1.25 row at $font-size).
.violations-inline
  min-height: calc($font-size * 1.25)
</style>
