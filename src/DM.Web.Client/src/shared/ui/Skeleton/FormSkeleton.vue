<script setup lang="ts">
/**
 * Loading twin of a form of a page (Form with FormField): a caption over a
 * field of one line per row, as far apart as FormField's, and the row of its
 * buttons under them. The last field may be a BBCodeEditor: its toolbar line
 * over its box, so the form that takes the skeleton's place moves nothing.
 */
withDefaults(
  defineProps<{
    /** Number of fields to show */
    fields?: number;
    /** Number of buttons in the row under them */
    actions?: number;
    /** The least height of the editor that is the last field (its min-height prop); none, a field of one line. */
    editorHeight?: number;
  }>(),
  { fields: 3, actions: 2, editorHeight: undefined },
);
</script>

<template>
  <div class="form-skeleton" aria-hidden="true">
    <div v-for="i in fields" :key="i" class="skeleton-field">
      <!-- A blank of the caption's font: the line is as tall as the caption's -->
      <div class="skeleton-caption">&nbsp;</div>
      <div v-if="editorHeight && i === fields" class="skeleton-editor">
        <!-- The toolbar's line, as tall as its mode switch: a blank of one tab -->
        <div class="skeleton-toolbar">
          <span class="skeleton-tab">&nbsp;</span>
        </div>
        <!-- The box: the text area and the 1px border on either side -->
        <div
          class="skeleton-editor-box"
          :style="{ height: `${editorHeight + 2}px` }"
        />
      </div>
      <div v-else class="skeleton-control" />
    </div>
    <div v-if="actions" class="skeleton-actions">
      <div v-for="i in actions" :key="i" class="skeleton-button" />
    </div>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Skeleton" as *
@use "@/assets/styles/Inputs" as *

.skeleton-field
  display: flex
  flex-direction: column
  margin: $small 0
  gap: $minor

.skeleton-caption
  width: $grid-step * 30
  +field-caption
  +skeleton-shimmer

.skeleton-control
  height: $control-height
  +skeleton-shimmer

// The line of BBCodeEditorToolbar: its padding round one tab of EditorModeSwitch.
.skeleton-toolbar
  padding: $small 0
  font-size: 0

.skeleton-tab
  display: inline-block
  width: $grid-step * 20
  vertical-align: top
  padding: $tiny $small
  font-size: $secondary-font-size
  border-bottom: 2px solid transparent
  +skeleton-shimmer

.skeleton-editor-box
  +skeleton-shimmer

.skeleton-actions
  display: flex
  gap: $small
  margin-top: $medium

.skeleton-button
  width: $grid-step * 28
  height: $control-height
  +skeleton-shimmer
</style>
