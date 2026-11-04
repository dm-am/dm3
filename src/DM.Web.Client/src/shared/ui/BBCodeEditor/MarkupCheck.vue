<script setup lang="ts">
/**
 * The markup check under the editor, in the BBCode mode: what the save path
 * would refuse or read otherwise than typed, one finding to a line, read as
 * "Строка 3: [b] не закрыт, добавьте [/b]". It checks a moment after the
 * typing stops, not on every key.
 *
 * Its root carries the id the field's aria-describedby names.
 */
import { ref, watch } from "vue";
import { symbols } from "@/shared/lib/utils/icons";
import {
  validateBBCode,
  type BBCodeIssue,
} from "@/shared/lib/bbcode/bbcodeWarnings";
import { type BBCodeContext } from "@/shared/lib/bbcode/bbcodeTagSet";

const props = defineProps<{
  /** The BBCode source. */
  text: string;
  /** Whether the BBCode mode is on screen: the check runs in it alone. */
  active: boolean;
  /** The surface the save path reads the text on. */
  context: BBCodeContext;
}>();

const findings = ref<BBCodeIssue[]>([]);
let pending: ReturnType<typeof setTimeout> | undefined;

watch([() => props.text, () => props.active], () => {
  clearTimeout(pending);
  if (!props.active) {
    findings.value = [];
    return;
  }
  pending = setTimeout(() => {
    findings.value = validateBBCode(props.text, props.context);
  }, 300);
});
</script>

<template>
  <div
    v-if="active && findings.length > 0"
    id="bbcode-validation-errors"
    class="validation-errors"
    role="alert"
    aria-live="polite"
    aria-atomic="true"
  >
    <div
      v-for="(issue, index) in findings"
      :key="`${index}:${issue.message}`"
      class="validation-error"
    >
      <span class="error-icon" aria-hidden="true">{{ symbols.warning }}</span>
      <span class="error-text">{{ issue.message }}</span>
    </div>
  </div>
</template>

<style scoped lang="sass">
// The overlay the site's controls sit on, not a colour: a fill was too loud for a note under the field.
.validation-errors
  padding: $small $medium
  background-color: $control-bg-overlay
  border-top: 1px solid
  border-color: $border

.validation-error
  display: flex
  align-items: center
  gap: $small
  padding: $tiny 0
  font-size: $secondary-font-size
  color: $accent-red

  .error-icon
    font-size: $tertiary-font-size

  .error-text
    flex: 1
</style>
