<script setup lang="ts">
/**
 * DiceComposer — the rolls a new post goes out with, laid out as the old site
 * lays them out, in the site's own controls.
 *
 * Over the line, the two boxes that hold for every roll ("Показывать всем
 * результаты бросков", "Броски взрываются максимум [ ] раз"). The line
 * itself: how many dice, "x", a button for each die of the old site (D3 D4 D6
 * D8 D10 D12 D20), "D" with a field of edges for any other die, D100, "+" and
 * the modifier, then "Комментарий" and its field over the rest of the width.
 * A die is thrown by its own button with what the line holds: the press adds
 * the roll, and the comment, the count and the modifier go back to empty, 1
 * and 0. "D" throws a die of the edges written in the field beside it, and
 * Enter in that field throws it too.
 *
 * Under the line, the rolls that will go out, in the words of the published
 * post without the result: the server throws when the post is sent, so a roll
 * here can still be taken back by its cross and cannot be thrown again.
 */
import { ref } from "vue";
import type { DiceRollInput } from "@/entities/game";
import { ActionResult } from "@/shared/ui/ActionResult";
import { symbols } from "@/shared/lib/utils/icons";
import { UNLIMITED_EXPLOSIONS, formatPendingRoll } from "./diceLine";

/** The dice of the old site's line before its field of edges, and after it. */
const DICE_BEFORE = [3, 4, 6, 8, 10, 12, 20] as const;
const DICE_AFTER = [100] as const;

// The bounds the server holds a roll to (CreatePostValidator): a roll out of
// them would refuse the whole post after the press of "Отправить".
const MAX_DICE = 100;
const MIN_EDGES = 2;
const MAX_EDGES = 1000;

// What a press out of those bounds says, the bounds written out.
const COUNT_REFUSAL = "Кубиков от 1 до 100";
const EDGES_REFUSAL = "Граней от 2 до 1000";

/** What a hidden roll says after its formula. */
const HIDDEN_NOTE = " (видит только мастер)";

const props = withDefaults(
  defineProps<{
    /** The rolls the post goes out with. */
    modelValue: DiceRollInput[];
    /** The post is on its way: nothing is added until it is answered. */
    disabled?: boolean;
  }>(),
  { disabled: false },
);

const emit = defineEmits<{
  "update:modelValue": [rolls: DiceRollInput[]];
}>();

// What holds for every roll until it is changed.
const isPublic = ref(true);
const explodes = ref(false);
const explosionLimit = ref<number | "">("");

// What the next roll takes, reset after every throw. The edges of "D" stay:
// they are the die, as D20 is, and not a part of one roll.
const count = ref<number | "">(1);
const bonus = ref<number | "">(0);
const comment = ref("");
const customEdges = ref<number | "">("");

/** Why the last press added nothing, under the line. */
const reason = ref<string | null>(null);

/** A number the field holds as a whole number, `fallback` when it is empty. */
function whole(value: number | "", fallback: number): number | null {
  if (value === "") return fallback;
  return Number.isInteger(value) ? value : null;
}

/**
 * The cap of explosions: none without the box, the server's own cap (no
 * limit) with the box and an empty field, the number written otherwise.
 */
function explosion(): number {
  if (!explodes.value) return 0;
  const limit = explosionLimit.value;
  if (limit === "" || !Number.isFinite(limit)) return UNLIMITED_EXPLOSIONS;
  return Math.min(UNLIMITED_EXPLOSIONS, Math.max(0, Math.trunc(limit)));
}

/** A number typed into "Броски взрываются максимум" means the dice explode. */
function onLimitInput() {
  if (explosionLimit.value !== "") explodes.value = true;
}

/** Adds a roll of a die of `edges`; false, with the reason, when it cannot. */
function throwDie(edges: number): boolean {
  const dice = whole(count.value, 1);
  if (dice === null || dice < 1 || dice > MAX_DICE) {
    reason.value = COUNT_REFUSAL;
    return false;
  }
  const modifier = whole(bonus.value, 0);
  if (modifier === null) {
    reason.value = "Модификатор пишется целым числом";
    return false;
  }
  reason.value = null;
  emit("update:modelValue", [
    ...props.modelValue,
    {
      dice: edges,
      count: dice,
      bonus: modifier,
      explosion: explosion(),
      public: isPublic.value,
      comment: comment.value.trim() || undefined,
    },
  ]);
  comment.value = "";
  count.value = 1;
  bonus.value = 0;
  return true;
}

/** "D": a die of the edges written in the field beside it. */
function throwCustom() {
  const edges = whole(customEdges.value, Number.NaN);
  if (edges === null || !(edges >= MIN_EDGES && edges <= MAX_EDGES)) {
    reason.value = EDGES_REFUSAL;
    return;
  }
  throwDie(edges);
}

function removeRoll(index: number) {
  emit(
    "update:modelValue",
    props.modelValue.filter((_, at) => at !== index),
  );
}
</script>

<template>
  <div class="dice-composer" role="group">
    <div class="dice-options">
      <label class="dice-check">
        <input v-model="isPublic" type="checkbox" :disabled="disabled" />
        Показывать всем результаты бросков
      </label>
      <label class="dice-check">
        <input v-model="explodes" type="checkbox" :disabled="disabled" />
        Броски взрываются максимум
        <input
          v-model.number="explosionLimit"
          type="number"
          min="0"
          :max="UNLIMITED_EXPLOSIONS"
          class="dice-number dice-number--short"
          aria-label="Сколько раз взрывается кубик"
          :disabled="disabled"
          @input="onLimitInput"
        />
        раз
      </label>
    </div>

    <!-- The line of the old site: the dice and their numbers, then the
         comment over the rest of the width, under the dice where the two do
         not fit side by side. -->
    <div class="dice-line">
      <div class="dice-roll" role="group" aria-label="Бросить кубик">
        <input
          v-model.number="count"
          type="number"
          min="1"
          :max="MAX_DICE"
          class="dice-number dice-number--short"
          aria-label="Сколько кубиков"
          :disabled="disabled"
        />
        <span class="dice-sign" aria-hidden="true">x</span>
        <button
          v-for="edges in DICE_BEFORE"
          :key="edges"
          type="button"
          class="dice-die"
          :aria-label="`Бросить D${edges}`"
          :disabled="disabled"
          @click="throwDie(edges)"
        >
          D{{ edges }}
        </button>
        <button
          type="button"
          class="dice-die"
          aria-label="Бросить кубик с числом граней из поля"
          :disabled="disabled"
          @click="throwCustom"
        >
          D
        </button>
        <input
          v-model.number="customEdges"
          type="number"
          :min="MIN_EDGES"
          :max="MAX_EDGES"
          class="dice-number"
          aria-label="Число граней"
          :disabled="disabled"
          @keydown.enter.prevent="throwCustom"
        />
        <button
          v-for="edges in DICE_AFTER"
          :key="edges"
          type="button"
          class="dice-die"
          :aria-label="`Бросить D${edges}`"
          :disabled="disabled"
          @click="throwDie(edges)"
        >
          D{{ edges }}
        </button>
        <span class="dice-sign" aria-hidden="true">+</span>
        <input
          v-model.number="bonus"
          type="number"
          class="dice-number"
          aria-label="Модификатор"
          :disabled="disabled"
        />
      </div>
      <label class="dice-comment-line">
        <span class="dice-comment-caption">Комментарий</span>
        <input
          v-model="comment"
          type="text"
          class="dice-comment"
          :disabled="disabled"
        />
      </label>
    </div>

    <ActionResult :text="reason" />

    <ol v-if="modelValue.length" class="dice-pending">
      <li v-for="(roll, index) in modelValue" :key="index">
        {{ formatPendingRoll(roll, index + 1)
        }}<span v-if="roll.public === false" class="dice-hidden">{{
          HIDDEN_NOTE
        }}</span
        ><span class="copy-space">{{ " " }}</span
        ><button
          type="button"
          class="dice-remove"
          :aria-label="`Убрать бросок ${index + 1}`"
          @click="removeRoll(index)"
        >
          {{ symbols.close }}
        </button>
      </li>
    </ol>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *
@use "@/assets/styles/Filters" as *

// The two boxes in one line, as on the old site, the second under the first
// where the line has no room for both. Neither breaks inside itself.
.dice-options
  display: flex
  flex-wrap: wrap
  align-items: center
  gap: $small $medium
  margin-bottom: $small

.dice-check
  display: flex
  align-items: center
  gap: $small
  color: $text
  cursor: pointer
  white-space: nowrap

// A field of the page (+input), as narrow as the digits it holds and with
// them in its middle, as the fields of the old site's line are; the arrows a
// browser draws in a number field would take half of it.
.dice-number
  +input()
  &
    width: 52px
    flex: none
    text-align: center
    appearance: textfield

  &::-webkit-outer-spin-button,
  &::-webkit-inner-spin-button
    -webkit-appearance: none
    margin: 0

// The count and the explosions hold three digits at most, the edges and the
// modifier four characters.
.dice-number--short
  &
    width: 44px

// The line: the dice and their numbers, and the comment beside them over the
// rest of the width (the old site's flex line, its 8 between the two parts and
// its 350 that the comment keeps before it goes under the dice).
.dice-line
  display: flex
  flex-wrap: wrap
  align-items: center
  gap: $small

// The parts of a roll stand as the words of the old site's line do, a space
// apart; where the column is narrower than the whole of it, they go on in a
// second line, every part whole.
.dice-roll
  display: flex
  flex-wrap: wrap
  align-items: center
  gap: $minor

.dice-sign
  color: $text

// A die is a button of the site (+button), as tight round its figure as the
// old site's, the figure in its bold and in the brown of the headings it was
// drawn in there.
.dice-die
  +button
  &
    padding: 0 $small
    color: $heading
    font-weight: 700

.dice-comment-line
  display: flex
  flex: 1 1 350px
  align-items: center
  gap: $small
  min-width: 0

// A caption of a field of the page: +field-caption, as FormField draws it.
.dice-comment-caption
  flex: none
  +field-caption

.dice-comment
  +input()
  &
    flex: 1
    min-width: $grid-step * 25

// The rolls that go out with the post, in the look of the published strip
// (GamePost.vue, .dice-rolls), on its ground.
.dice-pending
  list-style: none
  margin: $small 0 0
  padding: 2px 7px
  background-color: $bg-element-accent
  color: $text
  font-size: $font-size
  text-align: left

.dice-hidden
  color: $text-muted

.dice-remove
  margin-left: 6px
  vertical-align: middle
  +remove-icon-btn
</style>
