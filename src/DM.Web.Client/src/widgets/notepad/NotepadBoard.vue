<script setup lang="ts">
/** The two-pane notepad of the three notepads; a page supplies the endpoints and the words. */
import { computed, onMounted, ref, watch } from "vue";
import type { NotepadEntry } from "@/shared/api/models/notepads";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import PageTitle from "@/shared/ui/Layout/PageTitle.vue";
import HumanDate from "@/shared/ui/Date/HumanDate.vue";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { CaptionAction, CaptionLine } from "@/shared/ui/CaptionLine";
import type { NotepadAdapter } from "./types";
import { refusalHere } from "@/shared/lib/errors";
import { ErrorState } from "@/shared/ui/ErrorState";
import { Button } from "@/shared/ui/Button";
import NotepadEntryEditor from "./NotepadEntryEditor.vue";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";

const props = withDefaults(
  defineProps<{
    adapter: NotepadAdapter;
    /** Heading above the board. */
    title: string;
    /** Shown instead of the board when the viewer may not open this notepad. */
    accessible?: boolean;
    deniedText?: string;
    /** Empty state: the line and the sentence under it. */
    emptyText: string;
    emptyHint: string;
    /** The line in place of the list when the list request fails. */
    loadErrorText: string;
    /** Who is looking: an entry is edited by its author alone; null for a guest. */
    viewerId: string | null;
    /** The viewer leads the container and may delete an entry somebody else wrote. */
    canDeleteOthers?: boolean;
    /** Personal notepad: own page, h1 and page padding. */
    standalone?: boolean;
  }>(),
  {
    accessible: true,
    deniedText: "",
    canDeleteOthers: false,
    standalone: false,
  },
);

const entries = ref<NotepadEntry[]>([]);
const loading = ref(false);
/** Why the list did not arrive, in the place of the list with "Повторить". */
const loadRefusal = ref("");

const showEditor = ref(false);
const editingEntry = ref<NotepadEntry | null>(null);

/** Bumped on every opening, so each one mounts a fresh editor. */
const editorKey = ref(0);

const selectedId = ref<string | null>(null);
const selectedEntry = computed(
  () => entries.value.find((e) => e.id === selectedId.value) ?? null,
);

const pendingDelete = ref<NotepadEntry | null>(null);

const sortedEntries = computed(() =>
  [...entries.value].sort((a, b) => a.sortOrder - b.sortOrder),
);

// The client half of the server's rules: offered on every entry, they earned a 403.
const canEditSelected = computed(
  () =>
    !!selectedEntry.value &&
    !!props.viewerId &&
    selectedEntry.value.authorId === props.viewerId,
);

const canDeleteSelected = computed(
  () =>
    !!selectedEntry.value &&
    !!props.viewerId &&
    (selectedEntry.value.authorId === props.viewerId || props.canDeleteOthers),
);

async function fetchEntries() {
  if (!props.accessible) return;
  loading.value = true;
  loadRefusal.value = "";
  const { data, error } = await props.adapter.list();
  loading.value = false;
  if (error) {
    // An expired session and a dead connection keep their plate.
    loadRefusal.value =
      refusalHere(error, props.loadErrorText) ?? props.loadErrorText;
    return;
  }
  entries.value = data?.resources ?? [];
}

/** Opens the editor on `entry`, or on a new one. */
function openEditor(entry: NotepadEntry | null) {
  editingEntry.value = entry;
  editorKey.value += 1;
  showEditor.value = true;
}

function closeEditor() {
  showEditor.value = false;
  editingEntry.value = null;
}

/** The editor's saved entry, put in the list, and the editor closed. */
function onEntrySaved(saved: NotepadEntry, editing: NotepadEntry | null) {
  if (editing) {
    const index = entries.value.findIndex((e) => e.id === editing.id);
    if (index !== -1) entries.value[index] = saved;
  } else {
    entries.value.push(saved);
    // A created entry opens in the reader pane, as a created game opens on its page.
    selectedId.value = saved.id;
  }
  closeEditor();
}

/** The confirmation's action: it closes on success alone, which lets go of pendingDelete. */
async function deleteEntry() {
  const entry = pendingDelete.value;
  if (!entry) return;

  const result = await props.adapter.remove(entry.id, OWNS_REFUSAL);
  if (!result.error) {
    entries.value = entries.value.filter((e) => e.id !== entry.id);
  }
  return result;
}

// A listbox with a roving tabindex: one option in the Tab cycle, the selection follows focus.
const entryList = ref<HTMLElement | null>(null);

const tabbableEntryId = computed(
  () => selectedEntry.value?.id ?? sortedEntries.value[0]?.id ?? null,
);

function focusEntry(index: number) {
  const options =
    entryList.value?.querySelectorAll<HTMLElement>('[role="option"]');
  options?.[index]?.focus();
}

function onEntryKeydown(event: KeyboardEvent, index: number) {
  const list = sortedEntries.value;
  const last = list.length - 1;
  let nextIndex: number;

  switch (event.key) {
    case "ArrowDown":
      nextIndex = index === last ? 0 : index + 1;
      break;
    case "ArrowUp":
      nextIndex = index === 0 ? last : index - 1;
      break;
    case "Home":
      nextIndex = 0;
      break;
    case "End":
      nextIndex = last;
      break;
    case "Enter":
    case " ":
      nextIndex = index;
      break;
    default:
      return;
  }

  event.preventDefault();
  selectedId.value = list[nextIndex].id;
  focusEntry(nextIndex);
}

// Access comes from a container store and may flip to true once it loads.
watch(() => props.accessible, fetchEntries);

onMounted(fetchEntries);
</script>

<template>
  <div class="notepad" :class="{ standalone }">
    <div v-if="!accessible" class="notepad-denied">
      <secondary-text>{{ deniedText }}</secondary-text>
    </div>

    <template v-else>
      <div class="page-header">
        <page-title v-if="standalone">{{ title }}</page-title>
        <h2 v-else class="page-title">{{ title }}</h2>
        <!-- Secondary: with the editor open, "Сохранить" is the one main button -->
        <Button type="button" class="add-btn" @click="openEditor(null)">
          Новая запись
        </Button>
      </div>

      <!-- The twin of the loaded layout, sharing its classes so the tracks match -->
      <div v-if="loading" class="notepad-layout" aria-hidden="true">
        <div class="entry-list">
          <div v-for="i in 3" :key="i" class="skeleton-entry">
            <div class="skeleton-entry-title" />
            <div class="skeleton-entry-date" />
          </div>
        </div>
        <div class="entry-content">
          <div class="no-selection">
            <div class="skeleton-content-hint" />
          </div>
        </div>
      </div>

      <ErrorState
        v-else-if="loadRefusal && entries.length === 0 && !showEditor"
        :message="loadRefusal"
        :retry="fetchEntries"
      />

      <template v-else-if="entries.length === 0 && !showEditor">
        <secondary-text>{{ emptyText }}</secondary-text>
        <p class="hint">{{ emptyHint }}</p>
      </template>

      <div v-else class="notepad-layout">
        <div
          ref="entryList"
          class="entry-list"
          role="listbox"
          :aria-label="title"
        >
          <div
            v-for="(entry, index) in sortedEntries"
            :key="entry.id"
            class="entry-item"
            :class="{ selected: selectedEntry?.id === entry.id }"
            role="option"
            :aria-selected="selectedEntry?.id === entry.id"
            :tabindex="entry.id === tabbableEntryId ? 0 : -1"
            @click="selectedId = entry.id"
            @keydown="onEntryKeydown($event, index)"
          >
            <div class="entry-title">{{ entry.title }}</div>
            <div class="entry-date">
              <human-date :date="entry.modifiedUtc || entry.createdUtc" />
            </div>
          </div>
        </div>

        <div class="entry-content">
          <NotepadEntryEditor
            v-if="showEditor"
            :key="editorKey"
            :adapter="adapter"
            :entry="editingEntry"
            @saved="onEntrySaved"
            @cancel="closeEditor"
          />

          <template v-else-if="selectedEntry">
            <h2 class="content-title">{{ selectedEntry.title }}</h2>
            <!-- The caption and the controls on one line, not buttons beside the title -->
            <CaptionLine class="content-meta">
              <span
                >Создано: <human-date :date="selectedEntry.createdUtc"
              /></span>
              <span v-if="selectedEntry.modifiedUtc"
                >Изменено: <human-date :date="selectedEntry.modifiedUtc"
              /></span>
              <CaptionAction
                v-if="canEditSelected"
                @click="openEditor(selectedEntry)"
                >Редактировать</CaptionAction
              >
              <CaptionAction
                v-if="canDeleteSelected"
                @click="pendingDelete = selectedEntry"
                >Удалить</CaptionAction
              >
            </CaptionLine>
            <div class="content-body">
              <pre>{{ selectedEntry.content }}</pre>
            </div>
          </template>

          <div v-else class="no-selection">
            <secondary-text>Выберите запись для просмотра</secondary-text>
          </div>
        </div>
      </div>
    </template>

    <ConfirmDialog
      :show="pendingDelete !== null"
      title="Удалить запись?"
      :message="`Запись &quot;${pendingDelete?.title ?? ''}&quot; будет удалена без возможности восстановления`"
      confirm-label="Удалить"
      failure="Не удалось удалить запись"
      :action="deleteEntry"
      @update:show="(v) => !v && (pendingDelete = null)"
    />
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Skeleton" as *

.notepad
  display: flex
  flex-direction: column
  min-height: $grid-step * 50

  // The personal notepad is the page: no padding, the heading row where a page heading stands.
  &.standalone
    height: 100%
    min-height: 0

    .page-header
      margin: $medium 0

.notepad-denied
  padding: $big

.page-header
  display: flex
  justify-content: space-between
  align-items: center
  margin-bottom: $medium

  // PageTitle renders an h1, the tab variant an h2: both flush in the header.
  h1
    margin: 0

.page-title
  margin: 0

.hint
  color: $text-muted
  font-size: 0.9rem
  margin-top: $small

.notepad-layout
  display: grid
  grid-template-columns: 300px 1fr
  gap: $medium
  flex: 1
  min-height: 0

.entry-list
  border: 1px solid $border
  overflow-y: auto
  background: $bg-element

.entry-item
  padding: $small
  border-bottom: 1px solid $border
  cursor: pointer
  transition: background-color $transition-normal

  &:last-child
    border-bottom: none

  &:hover
    background: $hover-overlay

  &.selected
    background: $selected-overlay

  // The ring goes inward: an outset one would be clipped by the list's scroll box.
  &:focus-visible
    +focus-ring($inward: true)

// A long title wraps, whole, rather than losing its end under "...".
.entry-title
  font-weight: 400
  color: $text

.entry-date
  font-size: $tertiary-font-size
  color: $text-muted
  margin-top: $tiny

.entry-content
  border: 1px solid $border
  padding: $medium
  overflow-y: auto
  background: $bg-element

.no-selection
  display: flex
  align-items: center
  justify-content: center
  height: 100%

.content-title
  margin: 0 0 $small

// The caption line draws its own look; here it only stands as a block.
.content-meta
  display: block
  margin-bottom: $medium

.content-body
  pre
    white-space: pre-wrap
    overflow-wrap: break-word
    font-family: inherit
    margin: 0
    color: $text

// Twin of .entry-item: its bars mirror the title's and the date's line boxes.
.skeleton-entry
  padding: $small
  border-bottom: 1px solid $border

  &:last-child
    border-bottom: none

.skeleton-entry-title
  width: 70%
  height: 16px
  margin: $tiny 0
  +skeleton-shimmer

.skeleton-entry-date
  width: 90px
  height: 12px
  margin-top: $minor
  +skeleton-shimmer

// Twin of the centered "Выберите запись…" secondary-text line (14px).
.skeleton-content-hint
  width: 220px
  height: 14px
  +skeleton-shimmer
</style>
