<script setup lang="ts">
import { ref, reactive, toRef, watch } from "vue";
import { useExpandableSection } from "@/shared/lib/composables";
import { useModal } from "vue-final-modal";
import { symbols } from "@/shared/lib/utils/icons";
import type {
  ViolationSummary,
  ModerationPermissions,
} from "@/shared/api/models/moderation";
import type { Username } from "@/shared/api/models/community";
import {
  moderationApi,
  type PublicWarning,
  type PublicBan,
} from "@/entities/moderation";
import { WarningDialog, BanDialog } from "@/features/moderation-actions";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import Button from "@/shared/ui/Button/Button.vue";
import { formatDate, formatDateFull } from "@/shared/lib/utils/datetime";

// NOTE: moderationApi.getWarnings/getBans hit the PUBLIC endpoints
// (GET users/{username}/warnings|bans), which only return aggregate
// facts — no reason, no moderator identity (see PublicWarning/PublicBan).
// This mod-panel list used to assume the full moderation shape; that
// mismatch is out of scope here (owned by another unit) and is left as
// a pre-existing gap — only the TS types below were corrected to match
// what the API actually returns, so the fields referenced below that no
// longer exist (.reason, .moderator, .comment) render as empty/undefined
// instead of failing to compile.

const props = defineProps<{
  violations: ViolationSummary;
  permissions: ModerationPermissions;
  targetUsername: string;
}>();

const emit = defineEmits<{
  (e: "updated"): void;
}>();

// Expandable lists — content expandable sections (unified reveal
// animation + the page-wide "Развернуть/Свернуть все" toggle). The list
// data lazy-loads on first expand, whichever way the expand arrives.
const showWarnings = ref(false);
const showBans = ref(false);
const warningsList = ref<PublicWarning[] | null>(null);
const bansList = ref<PublicBan[] | null>(null);
const loadingWarnings = ref(false);
const loadingBans = ref(false);

const warningsZoneRef = ref<HTMLElement | null>(null);
const { toggle: toggleWarnings, zoneBindings: warningsZoneBindings } =
  useExpandableSection({
    el: warningsZoneRef,
    model: showWarnings,
    label: "ModerationWarningsList",
  });

const bansZoneRef = ref<HTMLElement | null>(null);
const { toggle: toggleBans, zoneBindings: bansZoneBindings } =
  useExpandableSection({
    el: bansZoneRef,
    model: showBans,
    label: "ModerationBansList",
  });

watch(showWarnings, async (open) => {
  if (open && warningsList.value === null) {
    loadingWarnings.value = true;
    const { data } = await moderationApi.getWarnings(
      props.targetUsername as Username,
    );
    warningsList.value = data?.warnings ?? [];
    loadingWarnings.value = false;
  }
});

watch(showBans, async (open) => {
  if (open && bansList.value === null) {
    loadingBans.value = true;
    const { data } = await moderationApi.getBans(
      props.targetUsername as Username,
    );
    bansList.value = data?.history ?? [];
    loadingBans.value = false;
  }
});

// --- Warning / ban dialogs (product doc 4.2.4.1 / 4.2.4.2) ---
// The profile moderation block (doc 4.2.2.19) is one of the seven warn-trigger
// sites. Both actions mount the spec-compliant dialogs from
// features/moderation-actions instead of a divergent inline form: WarningDialog
// offers verbal (0) / 1-6 points with a BBCode reason, BanDialog offers the
// Демократический/Полный access policy x 14 durations with a BBCode reason.
// A warning from the profile block is a general user warning (no specific
// content entity), so no entityId/entityType is passed.
const usernameRef = toRef(props, "targetUsername");

const { open: openWarnDialog, close: closeWarnDialog } = useModal({
  component: WarningDialog,
  attrs: reactive({
    username: usernameRef,
    onSuccess: () => {
      closeWarnDialog();
      // Force a refetch of the expandable list on next open + refresh the
      // moderation summary counts held by the parent.
      warningsList.value = null;
      emit("updated");
    },
    onCancel: () => closeWarnDialog(),
  }),
});

const { open: openBanDialog, close: closeBanDialog } = useModal({
  component: BanDialog,
  attrs: reactive({
    username: usernameRef,
    onSuccess: () => {
      closeBanDialog();
      bansList.value = null;
      emit("updated");
    },
    onCancel: () => closeBanDialog(),
  }),
});
</script>

<template>
  <div class="mod-section">
    <h4 class="mod-section_title">Нарушения</h4>

    <!-- Warnings summary + expandable list -->
    <div class="mod-violations_group">
      <button
        type="button"
        class="mod-violations_summary"
        :aria-expanded="showWarnings"
        @click="toggleWarnings()"
      >
        <span>
          Предупреждения: <strong>{{ violations.totalWarnings }}</strong>
        </span>
        <span
          v-if="violations.activeWarningPoints > 0"
          class="mod-warning-points"
        >
          ({{ violations.activeWarningPoints }} активных баллов)
        </span>
        <span class="mod-expand-icon" aria-hidden="true">{{
          showWarnings ? symbols.triangleDown : symbols.triangleRight
        }}</span>
      </button>

      <div
        ref="warningsZoneRef"
        class="expand-zone"
        v-bind="warningsZoneBindings"
      >
        <div v-if="showWarnings" class="mod-violations_list">
          <secondary-text v-if="loadingWarnings">Загрузка…</secondary-text>
          <template v-else-if="warningsList">
            <secondary-text v-if="warningsList.length === 0">
              Нет предупреждений
            </secondary-text>
            <div
              v-for="(w, wIndex) in warningsList"
              :key="wIndex"
              class="mod-violation-item"
              :class="{ 'mod-violation-active': w.isActive }"
            >
              <div class="mod-violation-item_header">
                <secondary-text>{{
                  formatDateFull(w.createdUtc)
                }}</secondary-text>
                <span class="mod-warning-badge">{{ w.points }} б.</span>
              </div>
            </div>
          </template>
        </div>
      </div>
    </div>

    <!-- Bans summary + expandable list -->
    <div class="mod-violations_group">
      <button
        type="button"
        class="mod-violations_summary"
        :aria-expanded="showBans"
        @click="toggleBans()"
      >
        <span>
          Баны: <strong>{{ violations.totalBans }}</strong>
        </span>
        <span v-if="violations.isCurrentlyBanned" class="mod-active-ban">
          (активный бан{{
            violations.currentBanEndUtc
              ? " до " + formatDate(violations.currentBanEndUtc)
              : " — перманентный"
          }})
        </span>
        <span class="mod-expand-icon" aria-hidden="true">{{
          showBans ? symbols.triangleDown : symbols.triangleRight
        }}</span>
      </button>

      <div ref="bansZoneRef" class="expand-zone" v-bind="bansZoneBindings">
        <div v-if="showBans" class="mod-violations_list">
          <secondary-text v-if="loadingBans">Загрузка…</secondary-text>
          <template v-else-if="bansList">
            <secondary-text v-if="bansList.length === 0">
              Нет банов
            </secondary-text>
            <div
              v-for="(b, bIndex) in bansList"
              :key="bIndex"
              class="mod-violation-item"
              :class="{ 'mod-violation-active': b.isActive }"
            >
              <div class="mod-violation-item_header">
                <secondary-text>{{
                  formatDateFull(b.startedUtc)
                }}</secondary-text>
                <span class="mod-ban-type">{{ b.type }}</span>
                <span v-if="b.expiresUtc">
                  до {{ formatDateFull(b.expiresUtc) }}
                </span>
              </div>
            </div>
          </template>
        </div>
      </div>
    </div>

    <!-- Action buttons — open the spec-compliant dialogs (doc 4.2.4.1/4.2.4.2) -->
    <div class="mod-violations_actions">
      <Button v-if="permissions.canIssueWarning" @click="openWarnDialog">
        Вынести предупреждение
      </Button>

      <Button v-if="permissions.canIssueBan" @click="openBanDialog">
        Забанить
      </Button>
    </div>
  </div>
</template>

<style scoped lang="sass">
.mod-section
  margin-bottom: $medium

.mod-section_title
  margin: 0 0 $small 0
  color: $heading

.mod-violations_group
  margin-bottom: $small

.mod-violations_summary
  width: 100%
  box-sizing: border-box
  border: none
  background: none
  font: inherit
  color: inherit
  text-align: left
  cursor: pointer
  padding: $minor 0
  display: flex
  align-items: baseline
  gap: $small
  &:hover
    color: $link-hover

.mod-expand-icon
  font-size: $tertiary-font-size

.mod-warning-points
  color: $accent-red
  font-size: $secondary-font-size

.mod-active-ban
  color: $accent-red
  font-size: $secondary-font-size
  font-weight: bold

.mod-violations_list
  padding-left: $medium
  margin-bottom: $small

.mod-violation-item
  padding: $small
  border-bottom: 1px solid $bg-element-accent
  &:last-child
    border-bottom: none

.mod-violation-active
  background: $bg-highlight-yellow

.mod-violation-item_header
  display: flex
  align-items: baseline
  gap: $small
  margin-bottom: $minor

.mod-violation-item_text
  white-space: pre-wrap
  word-break: break-word

.mod-warning-badge
  font-size: $secondary-font-size
  font-weight: bold
  color: $accent-red

.mod-ban-type
  font-size: $secondary-font-size
  font-weight: bold
  color: $accent-red

.mod-violations_actions
  display: flex
  gap: $small
  margin-top: $medium
</style>
