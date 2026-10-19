<script setup lang="ts">
/**
 * "Наблюдение": a switch a moderator throws by hand, served with the moderated
 * profile alone. A box saved on the press: held while the save is out, put
 * back with the reason on a refusal. Drawn only with the server's permission.
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

// Held, not disabled: a ticked box that cannot be ticked contradicts itself.
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
  // The flag is read from the parent's copy, so the parent refetches.
  emit("updated");
}
</script>

<template>
  <section v-if="permissions.canSetModerationWatch">
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
// The box and its caption 8 apart; a wrapped caption keeps the box at its first line.
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
