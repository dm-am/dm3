<script setup lang="ts">
// Fields in FormField's recipe, not FormField: a window's FormField rules reach nested ones by class.
import { onBeforeUnmount, onMounted, ref, useId } from "vue";
import { useAnchoredPanel } from "@/shared/lib/composables/useAnchoredPanel";
import { addressRefusal } from "@/shared/lib/utils/bbcodeAddresses";
import Button from "@/shared/ui/Button/Button.vue";

export interface InputField {
  name: string;
  label: string;
  type?: "text" | "url" | "email";
  placeholder?: string;
  required?: boolean;
  validator?: (value: string) => string | null; // Returns error message or null if valid
  /** What the field holds when the panel opens: the words selected in the text. */
  value?: string;
}

/** Between the button and the panel, and the least room kept at the screen's edge. */
const GAP = 4;

const props = defineProps<{
  /** The panel's name, the same as its button's ("Вставить ссылку"). */
  label: string;
  fields: InputField[];
  /** The toolbar button the panel hangs under, and where the caret goes back. */
  anchor: HTMLElement | null;
}>();

const emit = defineEmits<{
  (e: "submit", values: Record<string, string>): void;
  (e: "close"): void;
}>();

const values = ref<Record<string, string>>(
  Object.fromEntries(
    props.fields.map((field) => [field.name, field.value ?? ""]),
  ),
);
const errors = ref<Record<string, string>>({});
const panel = ref<HTMLElement | null>(null);
const position = ref<{ top: string; left: string }>({
  top: "0px",
  left: "0px",
});

/** Ids of this panel's fields: two editors on one page each open their own. */
const uid = useId();
const fieldId = (name: string) => `insert-${uid}-${name}`;
const errorId = (name: string) => `insert-${uid}-${name}-error`;

function validateField(field: InputField): boolean {
  const value = values.value[field.name] || "";

  // The field's own check speaks first: it can name what is wrong.
  const own = field.validator?.(value);
  if (own) {
    errors.value[field.name] = own;
    return false;
  }

  if (field.required && !value.trim()) {
    errors.value[field.name] = "Это поле обязательно";
    return false;
  }

  const address = field.type === "url" ? addressRefusal(value) : null;
  if (address) {
    errors.value[field.name] = address;
    return false;
  }

  delete errors.value[field.name];
  return true;
}

function handleSubmit() {
  // Every field is checked, so every refusal shows at once.
  const valid = props.fields
    .map((field) => validateField(field))
    .every(Boolean);
  if (valid) emit("submit", { ...values.value });
}

/** Closes the panel; `back` hands the caret to the button it hangs under. */
function close(back: boolean) {
  emit("close");
  if (back) props.anchor?.focus();
}

const { onFocusOut, closedByEscape } = useAnchoredPanel(
  panel,
  () => props.anchor,
  close,
);

function handleKeydown(event: KeyboardEvent) {
  if (closedByEscape(event)) return;
  // From a field only: Enter on a button is its own press. The default goes too, or a form around submits.
  if (
    event.key === "Enter" &&
    !event.shiftKey &&
    event.target instanceof HTMLInputElement
  ) {
    event.preventDefault();
    handleSubmit();
  }
}

/**
 * Under the button, left edges together, and moved left as far as it takes to
 * stay inside the editor. Above the button instead when the screen has no
 * room under it and more over it: the chat keeps its editor at the bottom.
 * Positioned from the editor's box, so the panel scrolls with the page and
 * nothing is recomputed on a scroll of it.
 */
function place() {
  const el = panel.value;
  const host = el?.offsetParent as HTMLElement | null | undefined;
  if (!el || !host || !props.anchor) return;
  const box = host.getBoundingClientRect();
  const button = props.anchor.getBoundingClientRect();
  const width = el.offsetWidth;
  const height = el.offsetHeight;
  const left = Math.max(
    0,
    Math.min(button.left - box.left, host.clientWidth - width),
  );
  const below = window.innerHeight - button.bottom;
  const above = button.top;
  const up = below < height + 2 * GAP && above > below;
  const top = up
    ? button.top - box.top - GAP - height
    : button.bottom - box.top + GAP;
  position.value = { top: `${top}px`, left: `${left}px` };
}

onMounted(() => {
  place();
  window.addEventListener("resize", place);
});

onBeforeUnmount(() => {
  window.removeEventListener("resize", place);
});
</script>

<template>
  <div
    ref="panel"
    class="insert-panel"
    role="dialog"
    :aria-label="label"
    tabindex="-1"
    :style="position"
    @keydown="handleKeydown"
    @focusout="onFocusOut"
  >
    <div
      v-for="field in fields"
      :key="field.name"
      class="insert-field"
      :class="{ error: errors[field.name] }"
    >
      <div class="insert-field-label">
        <label :for="fieldId(field.name)">{{ field.label }}</label>
        <span v-if="!field.required" class="insert-field-optional"
          >необязательно</span
        >
      </div>
      <input
        :id="fieldId(field.name)"
        v-model="values[field.name]"
        :name="field.name"
        :type="field.type || 'text'"
        :placeholder="field.placeholder"
        autocomplete="off"
        :aria-invalid="errors[field.name] ? 'true' : undefined"
        :aria-describedby="errors[field.name] ? errorId(field.name) : undefined"
        @blur="validateField(field)"
      />
      <span
        v-if="errors[field.name]"
        :id="errorId(field.name)"
        class="insert-field-error"
        role="alert"
        >{{ errors[field.name] }}</span
      >
    </div>
    <div class="insert-panel-actions">
      <Button type="button" variant="primary" @click="handleSubmit"
        >Вставить</Button
      >
      <Button type="button" @click="close(true)">Отмена</Button>
    </div>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Filters" as *
@use "@/assets/styles/Inputs" as *

.insert-panel
  +anchored-panel

// A refused field keeps a neutral frame, as every field's does: the red line says it.
.insert-field
  display: flex
  flex-direction: column
  gap: $minor
  margin: $small 0

// The role in the selector outweighs the rules of a form field the editor stands in.
.insert-panel[role="dialog"] .insert-field input
  box-sizing: border-box
  width: 100%
  +input()

.insert-field-label
  display: flex
  justify-content: space-between
  align-items: center
  color: $text
  font-size: $secondary-font-size
  font-weight: 700

.insert-field-optional
  flex-shrink: 0
  color: $text-muted
  font-weight: 400

.insert-field-error
  display: block
  margin-top: $minor
  color: $accent-red
  font-size: $secondary-font-size

.insert-panel-actions
  +anchored-panel-actions
</style>
