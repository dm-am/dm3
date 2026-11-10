<script setup lang="ts">
import type { FilterNavPath } from "@/shared/lib/composables/useFilterDropdown";
import { OptionsList } from "@/shared/ui/Filters";
import { useGamesFilter } from "../model";
import type {
  RecruitmentFilter,
  ClosedReasonFilter,
  StatusValue,
  GamesFilterKey,
} from "../model";

const navPath = defineModel<FilterNavPath<GamesFilterKey> | null>("navPath", {
  required: true,
});

const emit = defineEmits<{
  close: [];
}>();

const { setStatus, setRecruitmentFilter, setClosedReasonFilter } =
  useGamesFilter();

const statusOptions = [
  {
    value: "Draft",
    label: "Оформляется",
    hint: "Оформляющиеся игры",
    hasSubOptions: false,
  },
  {
    value: "Active",
    label: "Идет игра",
    hint: "Активные игры",
    hasSubOptions: true,
  },
  {
    value: "Closed",
    label: "Закрыта",
    hint: "Закрытые игры",
    hasSubOptions: true,
  },
];

const activeSubOptions = [
  { value: "any", label: "Все", hint: "Все активные игры" },
  {
    value: "recruiting",
    label: "Набор игроков",
    hint: "Игры с открытым набором",
    hasSubOptions: true,
  },
  { value: "closed", label: "Набор закрыт", hint: "Игры без набора" },
];

const recruitmentSubOptions = [
  { value: "open", label: "Все", hint: "Любой тип набора" },
  { value: "initial", label: "Первый набор", hint: "Новые игры, первый набор" },
  {
    value: "subsequent",
    label: "Донабор игроков",
    hint: "Продолжающиеся игры, повторный набор",
  },
];

const closedSubOptions = [
  { value: "any", label: "Все", hint: "Все закрытые игры" },
  { value: "None", label: "Без флагов", hint: "Игра закрыта" },
  {
    value: "Frozen",
    label: "Заморожена",
    hint: "Игра временно приостановлена",
  },
  { value: "Finished", label: "Завершена", hint: "Игра доведена до финала" },
];

function handleStatusSelect(value: string) {
  const option = statusOptions.find((o) => o.value === value);
  if (option?.hasSubOptions) {
    navPath.value = { filter: "status", status: value };
  } else {
    setStatus(value as StatusValue);
    emit("close");
  }
}

function handleActiveSubSelect(value: string) {
  if (value === "recruiting") {
    navPath.value = { filter: "status", status: "Active", recruitment: true };
  } else if (value === "any") {
    setStatus("Active");
    setRecruitmentFilter("any");
    emit("close");
  } else if (value === "closed") {
    setStatus("Active");
    setRecruitmentFilter("closed");
    emit("close");
  }
}

function handleRecruitmentSubSelect(value: string) {
  setStatus("Active");
  setRecruitmentFilter(value as RecruitmentFilter);
  emit("close");
}

function handleClosedSubSelect(value: string) {
  setStatus("Closed");
  setClosedReasonFilter(value as ClosedReasonFilter);
  emit("close");
}
</script>

<template>
  <OptionsList
    v-if="!navPath?.status"
    :options="statusOptions"
    @select="handleStatusSelect"
  />
  <OptionsList
    v-else-if="navPath.status === 'Active' && !navPath.recruitment"
    :options="activeSubOptions"
    @select="handleActiveSubSelect"
  />
  <OptionsList
    v-else-if="navPath.status === 'Active' && navPath.recruitment"
    :options="recruitmentSubOptions"
    @select="handleRecruitmentSubSelect"
  />
  <OptionsList
    v-else-if="navPath.status === 'Closed'"
    :options="closedSubOptions"
    @select="handleClosedSubSelect"
  />
</template>
