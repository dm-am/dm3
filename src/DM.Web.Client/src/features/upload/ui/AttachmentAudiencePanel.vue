<script setup lang="ts">
/**
 * "Кто видит карта.png": the characters to check, the room's and those the
 * file's mark keeps who left it, and the line a hidden block's tooltip would
 * say for them. The checks are this panel's until "Готово", which sends them
 * if they changed: none checked is every reader, "Отмена" leaves the mark as
 * it was.
 * The frame and the buttons are the editor's insertion panel's.
 */
import { computed, ref } from "vue";
import type { UploadAudience } from "@/shared/api/models/common/upload";
import { useAnchoredPanel } from "@/shared/lib/composables/useAnchoredPanel";
import { Button } from "@/shared/ui/Button";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { recipientsCaption } from "@/shared/lib/utils/bbcodePrivateRecipients";
import {
  audienceRecipients,
  everyReader,
  type AudienceCandidate,
} from "../model/attachmentAudience";

const props = defineProps<{
  fileName: string;
  audience: UploadAudience;
  candidates: readonly AudienceCandidate[];
  /** The word of the line the panel hangs under, and where the caret goes back. */
  anchor: HTMLElement | null;
}>();

const emit = defineEmits<{
  done: [audience: UploadAudience];
  close: [];
}>();

const panel = ref<HTMLElement | null>(null);

// The checks the panel opens with: the characters of the mark, each by his box.
const marked = props.candidates
  .map((c) => c.id)
  .filter((id) => props.audience.characterIds.includes(id));
const chosen = ref<string[]>([...marked]);

// The boxes checked, in their order. None checked is every reader, and a line
// naming the leads alone would say otherwise: it stands with a box checked.
const checked = computed(() =>
  props.candidates.map((c) => c.id).filter((id) => chosen.value.includes(id)),
);

const recipients = computed(() =>
  recipientsCaption(audienceRecipients(props.candidates, checked.value)),
);

function close(back: boolean) {
  emit("close");
  if (back) props.anchor?.focus();
}

const { onFocusOut, closedByEscape } = useAnchoredPanel(
  panel,
  () => props.anchor,
  close,
);

const same = (a: readonly string[], b: readonly string[]) =>
  a.length === b.length && a.every((id) => b.includes(id));

// Against the boxes the panel opened with: untouched, "Готово" sends nothing,
// not even every reader for a mark that checks no box.
function done() {
  if (!same(chosen.value, marked)) {
    const characterIds = checked.value;
    const audience: UploadAudience = characterIds.length
      ? { visibleTo: "Selected", characterIds }
      : everyReader();
    emit("done", audience);
  }
  close(true);
}
</script>

<template>
  <div
    ref="panel"
    class="audience-panel"
    role="dialog"
    :aria-label="`Кто видит ${fileName}`"
    tabindex="-1"
    @keydown="closedByEscape"
    @focusout="onFocusOut"
  >
    <div class="audience-caption">Кто видит {{ fileName }}</div>
    <div v-if="candidates.length" class="audience-characters">
      <label v-for="c in candidates" :key="c.id" class="choice">
        <input v-model="chosen" type="checkbox" :value="c.id" />
        {{ c.name }}
      </label>
    </div>
    <SecondaryText v-if="checked.length" class="audience-recipients">{{
      recipients
    }}</SecondaryText>
    <div class="audience-actions">
      <Button type="button" variant="primary" @click="done">Готово</Button>
      <Button type="button" @click="close(true)">Отмена</Button>
    </div>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Filters" as *
@use "@/assets/styles/FormRows" as *

// Under its line, at the left edge of the file's name.
.audience-panel
  +anchored-panel
  left: 0

// The caption of a field of the editor's panels.
.audience-caption
  margin: $small 0
  color: $text
  font-size: $secondary-font-size
  font-weight: 700
  overflow-wrap: anywhere

.audience-characters
  display: flex
  flex-direction: column
  gap: $tiny

.choice
  +choice

.audience-recipients
  margin-top: $small

.audience-actions
  +anchored-panel-actions
</style>
