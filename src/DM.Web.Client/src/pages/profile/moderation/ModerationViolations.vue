<script setup lang="ts">
/**
 * "Нарушения", the first section of the profile's "Модерация" tab: the two
 * counts and the two actions a moderator opens the tab for most often.
 *
 * The counts are stat lines of the profile ("Предупреждения: 2"), as in the
 * card at the top of the page, and each leads where its records can be read,
 * narrowed to this user. The warnings lead to "Последние предупреждения",
 * where every warning carries what it was given for. The bans lead to
 * "Последние баны", written as DM2's ban log (the owner, 12.10, line 45), with
 * the reason, the moderator and every lift. They used to open in place, a row
 * per ban from the public answer: no reason, no moderator, no lifted ban, under
 * a count that counted the lifted ones too ("Баны: 11" over ten rows). A count
 * of zero leads nowhere: there is nothing behind it.
 *
 * The buttons are the server's answer (permissions): a warning and a ban reach
 * only a rank below the reader's and never an administrator, so a moderator
 * sees neither on the profile of a colleague, a senior moderator or an
 * administrator.
 *
 * What is in force now (the active points, the current ban) is not repeated:
 * the card above the tabs says it to everybody, moderators included.
 */
import { reactive, toRef } from "vue";
import { useModal } from "vue-final-modal";
import type {
  ViolationSummary,
  ModerationPermissions,
} from "@/shared/api/models/moderation";
import { WarningDialog, BanDialog } from "@/features/moderation-actions";
import { BlockTitle } from "@/shared/ui/Layout";
import { StatLine } from "@/shared/ui/StatLine";
import Button from "@/shared/ui/Button/Button.vue";

const props = defineProps<{
  violations: ViolationSummary;
  permissions: ModerationPermissions;
  targetUsername: string;
}>();

const emit = defineEmits<{
  (e: "updated"): void;
}>();

/** "Последние предупреждения", narrowed to this user. */
const warningsLog = () => ({
  name: "moderation-warnings" as const,
  query: { username: props.targetUsername },
});

/** "Последние баны", narrowed to this user. */
const bansLog = () => ({
  name: "moderation-bans" as const,
  query: { username: props.targetUsername },
});

// --- Warning / ban dialogs (product doc 4.2.4.1 / 4.2.4.2) ---
// A warning issued from here is a warning on the profile: the server keeps the
// avatar, the status and "О себе" as they stand at that moment, and the log
// shows them with the warning (the owner, 12.10, line 41: "почему профиль
// пользователя не видно?"). The entity is the warned user, so no id is sent:
// the server takes the user the warning is for.
const usernameRef = toRef(props, "targetUsername");

// No toast from this dialog: the answer is on the screen behind it. The parent
// refetches the profile, and the count of warnings in this section goes up
// under the moderator's eyes (UI_STANDARDS, "Всплывающие сообщения"). The
// other openers of the same dialog - topic, chat message, comment list - keep
// their toast: nothing about a warning can appear on the page it was issued
// from there.
const { open: openWarnDialog, close: closeWarnDialog } = useModal({
  component: WarningDialog,
  attrs: reactive({
    username: usernameRef,
    entityType: "Profile",
    hideToast: true,
    onSuccess: () => {
      closeWarnDialog();
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
      // The counts are the parent's: it refetches them.
      emit("updated");
    },
    onCancel: () => closeBanDialog(),
  }),
});
</script>

<template>
  <section class="mod-section">
    <BlockTitle>Нарушения</BlockTitle>

    <div class="mod-violations_lines">
      <StatLine
        label="Предупреждения"
        :value="violations.totalWarnings"
        :to="violations.totalWarnings > 0 ? warningsLog() : undefined"
      />
      <StatLine
        label="Баны"
        :value="violations.totalBans"
        :to="violations.totalBans > 0 ? bansLog() : undefined"
      />
    </div>

    <!-- The dialogs are the site's own (doc 4.2.4.1/4.2.4.2). -->
    <div
      v-if="permissions.canIssueWarning || permissions.canIssueBan"
      class="mod-violations_actions"
    >
      <Button
        v-if="permissions.canIssueWarning"
        type="button"
        @click="openWarnDialog"
      >
        Выдать предупреждение
      </Button>
      <Button
        v-if="permissions.canIssueBan"
        type="button"
        @click="openBanDialog"
      >
        Выдать бан
      </Button>
    </div>
  </section>
</template>

<style scoped lang="sass">
// The rhythm of the profile's stat groups: $minor between the lines.
.mod-violations_lines
  display: flex
  flex-direction: column
  align-items: flex-start
  gap: $minor

.mod-violations_actions
  display: flex
  gap: $small
  margin-top: $medium
</style>
