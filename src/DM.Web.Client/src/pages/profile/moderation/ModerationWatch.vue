<script setup lang="ts">
/**
 * ModerationWatch — "Наблюдение", a section of the profile's "Модерация" tab
 * (doc 4.2.2.19).
 *
 * It lives here and not on "Нарушители" because the two answer different
 * questions. That page is a computed list: active warning points and active
 * bans, recomputed on every read and gone when they expire. This is a switch a
 * moderator throws by hand and nothing clears on its own, and both halves of it
 * — whether it is on, and whether the viewer may touch it — are served on the
 * moderated profile and nowhere else.
 *
 * A switch, so a box, saved the moment it is pressed, as the boxes of the
 * account settings are: the box itself says whether the watch is on, and the
 * sentence beside it says what that does. It used to be a sentence about the
 * state, a paragraph about the effect and a button naming the opposite state,
 * three things to read for one bit. While the save is out the box stays drawn
 * as pressed and further presses are held; a refusal puts it back and says why
 * under it.
 *
 * The block gates itself on the permission the server sends: the endpoint is
 * Moderator+ while the tab around this block opens wider, and a control the
 * server would answer 403 to is a control this tab must not draw.
 *
 * The word the domain uses for the people this is aimed at does not appear in
 * the interface. What a moderator needs to know is what the switch does: from
 * now on this user's new games and blogs start in premoderation.
 */
import { ref, watch } from "vue";
import type { ModerationPermissions } from "@/shared/api/models/moderation";
import type { Username } from "@/shared/api/models/community";
import { moderationApi } from "@/entities/moderation";
import { BlockTitle, SecondaryText } from "@/shared/ui/Layout";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";

const props = defineProps<{
  /** Whether the watch is on right now. */
  underWatch: boolean;
  permissions: ModerationPermissions;
  targetUsername: string;
}>();

const emit = defineEmits<{
  (e: "updated"): void;
}>();

/** The box as drawn: the parent's copy, or the press while it is saved. */
const checked = ref(props.underWatch);
watch(
  () => props.underWatch,
  (on) => (checked.value = on),
);

const saving = ref(false);

/** A refused press, under the box. */
const result = useActionResult();

// Held rather than disabled while the save is out: a ticked box that cannot be
// ticked is a contradiction on screen (savingCheckboxes.spec.ts).
function holdWhileSaving(event: Event) {
  if (saving.value) event.preventDefault();
}

async function toggleWatch(on: boolean) {
  checked.value = on;
  saving.value = true;
  result.clear();
  const { error } = await moderationApi.setModerationWatch(
    props.targetUsername as Username,
    on,
  );
  saving.value = false;
  if (error) {
    checked.value = !on;
    result.refuse(
      error,
      on
        ? "Не удалось взять пользователя под наблюдение"
        : "Не удалось снять наблюдение с пользователя",
    );
    return;
  }
  // The endpoint answers with the rebuilt profile, but the flag is read from
  // the parent's copy, so the parent is the one that has to refetch.
  emit("updated");
}
</script>

<template>
  <section v-if="permissions.canSetModerationWatch" class="mod-section">
    <BlockTitle>Наблюдение</BlockTitle>

    <label class="mod-watch_toggle">
      <input
        type="checkbox"
        :checked="checked"
        :aria-busy="saving || undefined"
        aria-describedby="mod-watch-note"
        @click="holdWhileSaving"
        @change="toggleWatch(($event.target as HTMLInputElement).checked)"
      />
      <span>Новые игры и блоги пользователя начинают с премодерации</span>
    </label>
    <SecondaryText id="mod-watch-note" class="mod-watch_note"
      >Наблюдение не снимается само.</SecondaryText
    >
    <ActionResult :result="result" />
  </section>
</template>

<style scoped lang="sass">
// The box and its caption: 16 through 8, as every checkbox of a page. A
// caption that wraps on a phone keeps the box against its first line.
.mod-watch_toggle
  display: inline-flex
  align-items: flex-start
  gap: $small
  font-size: $font-size
  color: $text
  cursor: pointer

  input
    flex-shrink: 0
    margin: $tiny 0 0

.mod-watch_note
  margin-top: $minor
</style>
