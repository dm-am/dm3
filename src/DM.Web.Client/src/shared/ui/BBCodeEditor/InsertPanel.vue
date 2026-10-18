<script setup lang="ts">
/**
 * The panel an insertion of the markup editor opens under its toolbar button:
 * a link, an image, a spoiler caption, a hidden block's readers.
 *
 * It has been a window over the whole page twice, and twice the
 * window was too much for a line of text: it dimmed the very text the piece
 * was going into (owner's call, 28 September 2026, the panel back). The panel
 * leaves the page alive: nothing is dimmed, the text of the editor stays in
 * sight, Tab walks out onto the page. Escape and "Отмена" close it and hand
 * the caret back to the button; a press anywhere else closes it and leaves the
 * caret where the reader put it. Enter from a field inserts. What cannot be
 * inserted is said under its field, in the panel itself.
 *
 * It stands in the markup next to the editor and is never carried to <body>:
 * in a window of the site, the window's focus trap takes the caret back from
 * anything outside it, and the fields could not be typed into.
 *
 * The fields are drawn here in the recipe of a field on a page (FormField: the
 * caption bold at the secondary size over the field, "необязательно" in the
 * regular weight beside it) and not by FormField itself. In a window the
 * editor stands inside the window's own FormField, whose window rules reach
 * every FormField nested in it by class, and the panel came out with the
 * window's captions at 15 and fields at 52. The buttons are a page's too: the
 * main one filled, "Отмена" beside it.
 */
import { nextTick, onBeforeUnmount, onMounted, ref } from "vue";
import { fieldToFocus } from "@/shared/lib/composables/useDialogShell";
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

/**
 * What an address may be: http and https, the two schemes the server shows a
 * link or an image from. Anything else stays out, javascript: and data: above
 * all, which would run in the reader's page, and mailto:, which the server
 * drops without a word. The check is on the type of the field, so it holds
 * the address of an image as it holds the address of a link.
 */
const URL_SCHEMES = ["http:", "https:"];

/** Between the button and the panel, and the least room kept at the screen's edge. */
const GAP = 4;

const props = withDefaults(
  defineProps<{
    /** The panel's name, the same as its button's ("Вставить ссылку"). */
    label: string;
    fields: InputField[];
    /** The toolbar button the panel hangs under, and where the caret goes back. */
    anchor: HTMLElement | null;
    submitLabel?: string;
    cancelLabel?: string;
  }>(),
  {
    submitLabel: "Вставить",
    cancelLabel: "Отмена",
  },
);

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
const uid = Math.random().toString(36).slice(2, 9);
const fieldId = (name: string) => `insert-${uid}-${name}`;
const errorId = (name: string) => `insert-${uid}-${name}-error`;

function validateField(field: InputField): boolean {
  const value = values.value[field.name] || "";

  // The field's own check speaks first. It knows what the field is and can
  // name it; the shared check after it can only say that something is
  // missing, and speaking first it silenced every validator written for an
  // empty value.
  const own = field.validator?.(value);
  if (own) {
    errors.value[field.name] = own;
    return false;
  }

  if (field.required && !value.trim()) {
    errors.value[field.name] = "Это поле обязательно";
    return false;
  }

  if (field.type === "url" && value.trim()) {
    try {
      const url = new URL(value);
      if (!URL_SCHEMES.includes(url.protocol)) {
        errors.value[field.name] = "Разрешены только адреса http и https";
        return false;
      }
    } catch {
      errors.value[field.name] = "Введите корректный URL";
      return false;
    }
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

function handleKeydown(event: KeyboardEvent) {
  if (event.key === "Escape") {
    // The panel is the top layer: its Escape goes no further, so a window the
    // editor stands in stays open (UI_STANDARDS.md, "Dialog Standards", ESC).
    event.preventDefault();
    event.stopPropagation();
    close(true);
    return;
  }
  // From a field only. On a button Enter is that button's own press, and
  // "Отмена" with the caret on it used to insert instead of leaving. The
  // default is taken too: the editor may stand in a form of the page, and
  // Enter in a field submits the form around it.
  if (
    event.key === "Enter" &&
    !event.shiftKey &&
    event.target instanceof HTMLInputElement
  ) {
    event.preventDefault();
    handleSubmit();
  }
}

/** A press on the button itself is the button's: it opens and closes the panel. */
const isOwn = (target: EventTarget | null): boolean =>
  target instanceof Node &&
  (!!panel.value?.contains(target) || !!props.anchor?.contains(target));

function onPressOutside(event: Event) {
  if (!isOwn(event.target)) close(false);
}

// Tab out of the last field, or Shift+Tab out of the first: the reader has
// gone on to the page, and the panel does not stay behind over the text.
function onFocusOut(event: FocusEvent) {
  const next = event.relatedTarget;
  if (next && !isOwn(next)) close(false);
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
  nextTick(() => {
    if (panel.value) (fieldToFocus(panel.value) ?? panel.value).focus();
  });
  document.addEventListener("pointerdown", onPressOutside, true);
  window.addEventListener("resize", place);
});

onBeforeUnmount(() => {
  document.removeEventListener("pointerdown", onPressOutside, true);
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
      <!-- The caption on the left, "необязательно" grey at the right end of
           the row, as FormField has it. A required field carries no mark. -->
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
      <Button type="button" variant="primary" @click="handleSubmit">{{
        submitLabel
      }}</Button>
      <Button type="button" @click="close(true)">{{ cancelLabel }}</Button>
    </div>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Filters" as *
@use "@/assets/styles/Inputs" as *
@use "@/assets/styles/ZIndex" as *

// The look of every floating panel of the site, =dropdown-panel: its ground,
// frame, rounding and shadow. Where it stands, how wide and how tall it is
// are the panel's own: under its button, as wide as an address needs, and as
// tall as its fields, with nothing to scroll. 360 holds the longest caption,
// "Имена персонажей через запятую", on one line with "необязательно".
.insert-panel
  +dropdown-panel
  right: auto
  z-index: $z-popover
  box-sizing: border-box
  width: 360px
  max-width: 100%
  max-height: none
  overflow: visible
  padding: $small $medium $medium
  outline: none

// A field on a page, FormField's recipe: the caption over the field, the
// field across the panel, and the refusal in red under it. The frame of a
// refused field stays neutral, as every field's does (FormField.vue): the red
// line says it, and a red frame said the same thing twice.
.insert-field
  display: flex
  flex-direction: column
  gap: $minor
  margin: $small 0

// The field itself is the site's field (+input), restated at the weight of the
// panel. The editor stands in a field of a form, the reason of a warning, and
// that field's rules reach every input under it: at 52 high in a window. The
// panel's fields are a page's, and the role in the selector is what outweighs
// them.
.insert-panel[role="dialog"] .insert-field input
  box-sizing: border-box
  width: 100%
  +input()

// The caption of a field on a page: bold at the secondary size, "необязательно"
// beside it in the regular weight (FormField.vue). It was the weight of the
// text here, and the panel's captions read weaker than every form's.
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
  display: flex
  gap: $small
  margin-top: $medium
</style>
