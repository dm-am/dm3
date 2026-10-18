<script setup lang="ts">
/**
 * BlacklistEditor — the one blacklist of the site, as the game, the blog and
 * the account show it. What each list closes, the requests and the reload
 * stay with the section that holds it; this draws the list.
 *
 * It is a table whose rows are added and removed in place, drawn after the
 * editor of a character sheet (AttributeSchemaEditor): the site's table,
 * "Пользователь", the bin at the end of each row, and the last line of the
 * table is where a name goes on, the field and "Добавить" (BlacklistAddRow),
 * not a window. That line is drawn under the box the table stands in rather
 * than in its foot (see the styles). The account's entries carry the day the
 * name went on, in a column of its own ("Внесен"); the game and the blog do
 * not have it. Over the table the game and the blog say in a grey line what
 * the list does.
 *
 * `add` and `remove` send the request and resolve to its failure, or null once
 * the list holds the change. A refused removal is said under its row, across
 * the table; a refused "Добавить" under the field, the name kept in it. The bin
 * asks nothing (ACC-241): the name goes back on with the field.
 *
 * A list that did not come is not an empty one: the section hands the line its
 * store wrote, and it stands where "В черном списке пока никого нет" would.
 * The field stays, and a name put on reads the list again.
 *
 * The field does not suggest the names the server would refuse: the viewer,
 * the names on the list already, and `exclude` - the people who run the game
 * or the blog.
 */
import { computed, ref } from "vue";
import { storeToRefs } from "pinia";
import { UserLink, useAuthStore } from "@/entities/user";
import type { GeneralError } from "@/shared/api/models/common";
import { refusalHere } from "@/shared/lib/errors";
import { formatDate } from "@/shared/lib/utils/datetime";
import { ActionResult } from "@/shared/ui/ActionResult";
import { DataTable, type Column } from "@/shared/ui/DataTable";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { RowIconButton } from "@/shared/ui/RowIconButton";
import type { BlacklistRow } from "../model/types";
import BlacklistAddRow from "./BlacklistAddRow.vue";

const props = defineProps<{
  /** What being on this list does, one line over the table. */
  hint?: string;
  rows: BlacklistRow[];
  loading: boolean;
  /** Draw the day each name went on ("Внесен"): the account's list has it. */
  dated?: boolean;
  /** Why the list is not on screen: it did not come. */
  error?: string | null;
  add: (username: string) => Promise<GeneralError | null>;
  remove: (username: string) => Promise<GeneralError | null>;
  /** Names the field does not suggest besides the viewer and the rows. */
  exclude?: readonly string[];
}>();

const NAME_COLUMN: Column = { key: "user", label: "Пользователь" };
// The share of "Дата" in the uploads table.
const DATE_COLUMN: Column = {
  key: "created",
  label: "Внесен",
  width: "18%",
  align: "center",
};
// As wide as its sign: the name takes the rest.
const ACTION_COLUMN: Column = {
  key: "action",
  label: "",
  width: "1px",
  align: "center",
};

const columns = computed(() =>
  props.dated
    ? [NAME_COLUMN, DATE_COLUMN, ACTION_COLUMN]
    : [NAME_COLUMN, ACTION_COLUMN],
);

const { user: viewer } = storeToRefs(useAuthStore());

const suggestNot = computed(() => [
  ...(viewer.value ? [viewer.value.username] : []),
  ...props.rows.map((row) => row.username),
  ...(props.exclude ?? []),
]);

/**
 * The names whose removal is on the wire. Their bins are shut until the
 * answer, so a second press sends nothing; the other rows stay live.
 */
const removing = ref(new Set<string>());

/** The last refused removal, under the row of the name it was about. */
const removeRefusal = ref<{ id: string; text: string } | null>(null);

async function submitRemove(row: BlacklistRow) {
  if (removing.value.has(row.username)) return;
  removing.value.add(row.username);
  removeRefusal.value = null;
  const error = await props.remove(row.username);
  removing.value.delete(row.username);
  if (!error) return;
  const text = refusalHere(error, "Пользователь не удален из черного списка");
  if (text) removeRefusal.value = { id: row.id, text };
}
</script>

<template>
  <div class="blacklist">
    <SecondaryText v-if="hint" class="bl-hint">{{ hint }}</SecondaryText>

    <div class="bl-table">
      <DataTable
        :columns="columns"
        :data="rows"
        :loading="loading"
        :details-for="removeRefusal?.id ?? null"
        empty-text="В черном списке пока никого нет"
        aria-label="Черный список"
      >
        <template #cell-user="{ row }">
          <UserLink v-if="row.user" :user="row.user" /><RouterLink
            v-else
            :to="{ name: 'profile', params: { username: row.username } }"
            class="user-link"
            >{{ row.username }}</RouterLink
          >
        </template>
        <template #cell-created="{ row }">
          {{ row.createdUtc ? formatDate(row.createdUtc) : "" }}
        </template>
        <template #cell-action="{ row }">
          <RowIconButton
            icon="trash"
            :label="`Удалить ${row.username} из черного списка`"
            :disabled="removing.has(row.username)"
            :aria-busy="removing.has(row.username) || undefined"
            @click="submitRemove(row)"
          />
        </template>
        <template v-if="error && !loading" #empty>
          <ActionResult :text="error" class="bl-load-refusal" />
        </template>
        <template #details>
          <ActionResult :text="removeRefusal?.text" class="bl-refusal" />
        </template>
      </DataTable>
      <div class="bl-foot">
        <BlacklistAddRow :add="add" :exclude="suggestNot" />
      </div>
    </div>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Tables" as *

// The steps of the site's settings: the line and the table it is about, one
// step of 16 apart.
.blacklist
  display: flex
  flex-direction: column
  gap: $medium

// The last line of the table, drawn as the foot of the site's table is (a cell
// of its padding and fill in the frame of $border the cells stand in), under
// the box the table scrolls sideways in rather than inside it: a box that
// scrolls one way clips the other too, and the suggestions of the field open
// down past the bottom of the table. As wide as the box, as the table is
// wherever it fits.
.bl-foot
  border: $table-gap solid $border
  border-top: none
  padding: $table-cell-padding-v $table-cell-padding-h
  background-color: $bg-element-accent

// The details row and the line of the empty table have the padding of a cell
// already.
.bl-refusal,
.bl-load-refusal
  --action-result-margin: 0
</style>
