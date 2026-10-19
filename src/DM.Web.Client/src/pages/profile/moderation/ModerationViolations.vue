<script setup lang="ts">
/**
 * "Нарушения" of the "Модерация" tab: the two counts, each leading to its log
 * narrowed to this user (none from zero), and the warning and the ban the
 * server's permissions allow. What is in force now is the card's above.
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

// Two warnings start here. One on the profile: the server keeps the avatar, the status and
// "О себе" with it. One with no place, for what was done where nothing is moderated (a
// conversation, a game room): no copy and no link. The server takes the user from the name.
const usernameRef = toRef(props, "targetUsername");

// No toast: the parent refetches and the count goes up on the screen behind.
const warnDialog = (entityType?: "Profile") => {
  const modal = useModal({
    component: WarningDialog,
    attrs: reactive({
      username: usernameRef,
      entityType,
      hideToast: true,
      onSuccess: () => {
        modal.close();
        emit("updated");
      },
      onCancel: () => modal.close(),
    }),
  });
  return modal;
};
const profileWarnDialog = warnDialog("Profile");
const placelessWarnDialog = warnDialog();

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
  <section>
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

    <!-- The site's own dialogs. -->
    <div
      v-if="permissions.canIssueWarning || permissions.canIssueBan"
      class="mod-violations_actions"
    >
      <template v-if="permissions.canIssueWarning">
        <Button type="button" @click="profileWarnDialog.open">
          Выдать предупреждение за профиль
        </Button>
        <Button type="button" @click="placelessWarnDialog.open">
          Выдать предупреждение без места
        </Button>
      </template>
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
