<script setup lang="ts">
// The server throws when the post is sent, so a roll here can be taken back but not rethrown.
import { ref } from "vue";
import type { DiceRollInput } from "../model/diceTypes";
import { ActionResult } from "@/shared/ui/ActionResult";
import { symbols } from "@/shared/lib/utils/icons";
import { UNLIMITED_EXPLOSIONS, formatPendingRoll } from "../model/diceLine";

const DICE_BEFORE = [3, 4, 6, 8, 10, 12, 20] as const;
const DICE_AFTER = [100] as const;

// CreatePostValidator: a roll out of these bounds refuses the whole post on send.
// ClientFieldLimitsShould compares the numbers.
const MAX_DICE = 100;
const MIN_EDGES = 2;
const MAX_EDGES = 1000;

const COUNT_REFUSAL = "Кубиков от 1 до 100";
const EDGES_REFUSAL = "Граней от 2 до 1000";

const HIDDEN_NOTE = " (видит только мастер)";

const props = withDefaults(
  defineProps<{
    modelValue: DiceRollInput[];
    /** The post is on its way. */
    disabled?: boolean;
  }>(),
  { disabled: false },
);

const emit = defineEmits<{
  "update:modelValue": [rolls: DiceRollInput[]];
}>();

const isPublic = ref(true);
const explodes = ref(false);
const explosionLimit = ref<number | "">("");

// Reset after every throw, except the edges of "D", which are the die as D20 is.
const count = ref<number | "">(1);
const bonus = ref<number | "">(0);
const comment = ref("");
const customEdges = ref<number | "">("");

const reason = ref<string | null>(null);

/** A number the field holds as a whole number, `fallback` when it is empty. */
function whole(value: number | "", fallback: number): number | null {
  if (value === "") return fallback;
  return Number.isInteger(value) ? value : null;
}

/** An empty field with the box means the server's own cap, no limit. */
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
      comment: comment.value.trim() ? comment.value : undefined,
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
@use "@/assets/styles/Buttons" as *
@use "@/assets/styles/Filters" as *

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

// The browser's arrows would take half of a field this narrow.
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

.dice-number--short
  &
    width: 44px

.dice-line
  display: flex
  flex-wrap: wrap
  align-items: center
  gap: $small

.dice-roll
  display: flex
  flex-wrap: wrap
  align-items: center
  gap: $minor

.dice-sign
  color: $text

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

.dice-comment-caption
  flex: none
  +field-caption

.dice-comment
  +input()
  &
    flex: 1
    min-width: $grid-step * 25

// In the look of the published strip, DiceRolls.vue.
.dice-pending
  list-style: none
  margin: $small 0 0
  padding: $tiny 7px
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
