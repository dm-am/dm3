<script setup lang="ts">
// The facts of a game, its two character rosters and its description.
import { computed } from "vue";
import { storeToRefs } from "pinia";
import {
  useGameDetailsStore,
  characterStatusLabel,
  GameStatusBadge,
  type Character,
} from "@/entities/game";
import {
  RemovedAccountLink,
  UserLink,
  UserRating,
  useUserDisplay,
} from "@/entities/user";
import { ContentText } from "@/shared/ui/Content";
import { DataTable, type Column } from "@/shared/ui/DataTable";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import { DashSeparator } from "@/shared/ui/DashSeparator";
import { formatDate, formatDateFull } from "@/shared/lib/utils/datetime";
import { VALUE_UNAVAILABLE } from "@/shared/lib/constants/copy";

const gameStore = useGameDetailsStore();
const { game, characters } = storeToRefs(gameStore);
const { isOnline } = useUserDisplay();

const assistants = computed(() => game.value?.assistants ?? []);
const readers = computed(() => game.value?.readers ?? []);
const tags = computed(() => game.value?.tags ?? []);

const playerCount = computed(() => {
  const r = game.value?.recruitment;
  const accepted = r?.pcCount ?? game.value?.players?.length ?? 0;
  return r?.pcLimit ? `${accepted}/${r.pcLimit}` : String(accepted);
});

function isInGame(c: Character): boolean {
  return c.status === "Active" || c.status === "Retired";
}
const playerCharacters = computed(() =>
  characters.value.filter((c) => !c.isNpc && isInGame(c)),
);
const npcCharacters = computed(() =>
  characters.value.filter((c) => c.isNpc && isInGame(c)),
);

const descriptorTitle = computed(
  () =>
    game.value?.schema?.specifications.find((s) => s.isDescriptor)?.title ||
    "Класс",
);
function descriptorOf(c: Character): string {
  return c.descriptor?.trim() || VALUE_UNAVAILABLE;
}

function lastPostOf(c: Character): string {
  return c.lastPostUtc ? formatDateFull(c.lastPostUtc) : VALUE_UNAVAILABLE;
}

function isRetired(c: Character): boolean {
  return c.status === "Retired";
}

function characterLink(c: Character) {
  return {
    name: "game-character",
    params: {
      id: game.value?.publicId,
      characterId: c.id,
    },
  };
}

// Not green: a character in the game is the plain state, not a good one.
function statusClass(c: Character): string {
  return c.status === "Active" ? "status-active" : "status-retired";
}

const centred = (columns: Column[]) =>
  columns.map((column) => ({ ...column, align: "center" as const }));

// A date and "Присутствие" need fixed pixels no percentage gives at every
// width, so they and the names go width-less and the rest keep proportions.
const rosters = computed(() => [
  {
    title: "Персонажи игроков",
    characters: playerCharacters.value,
    columns: centred([
      { key: "player", label: "Игрок" },
      { key: "rating", label: "Рейтинг", width: "11%" },
      { key: "presence", label: "Присутствие" },
      { key: "character", label: "Имя персонажа" },
      { key: "descriptor", label: descriptorTitle.value, width: "11%" },
      { key: "posts", label: "Посты", width: "7%" },
      { key: "lastpost", label: "Последний пост" },
      { key: "status", label: "Статус", width: "11%" },
    ]),
  },
  {
    title: "Неигровые персонажи",
    characters: npcCharacters.value,
    columns: centred([
      { key: "character", label: "Имя персонажа" },
      { key: "descriptor", label: descriptorTitle.value, width: "18%" },
      { key: "posts", label: "Посты", width: "12%" },
      { key: "lastpost", label: "Последний пост" },
      { key: "status", label: "Статус", width: "18%" },
    ]),
  },
]);
</script>

<template>
  <div v-if="game" class="game-details">
    <table class="info-table">
      <tbody>
        <tr>
          <th>Статус</th>
          <td>
            <GameStatusBadge
              :status="game.status"
              :is-recruiting="game.recruitment?.isOpen"
              :is-subsequent="game.recruitment?.isSubsequent"
              :closed-reason="game.closedReason"
            />
          </td>
        </tr>
        <tr>
          <th>Дата создания</th>
          <td>{{ formatDate(game.createdUtc) }}</td>
        </tr>
        <tr v-if="game.activatedUtc">
          <th>Дата начала</th>
          <td>{{ formatDate(game.activatedUtc) }}</td>
        </tr>
        <tr v-if="game.status === 'Closed' && game.closedUtc">
          <th>Дата завершения</th>
          <td>{{ formatDate(game.closedUtc) }}</td>
        </tr>
        <tr>
          <th>Игроков</th>
          <td>{{ playerCount }}</td>
        </tr>
        <tr>
          <th>Мастер</th>
          <td>
            <UserLink v-if="game.master" :user="game.master" hide-badge />
            <RemovedAccountLink v-else />
          </td>
        </tr>
        <tr>
          <th>{{ assistants.length > 1 ? "Ассистенты" : "Ассистент" }}</th>
          <td>
            <template v-if="assistants.length"
              ><template v-for="(a, i) in assistants" :key="a.id"
                ><UserLink :user="a" hide-badge /><span
                  v-if="i < assistants.length - 1"
                  >,
                </span></template
              ></template
            >
            <span v-else class="muted">нет</span>
          </td>
        </tr>
        <tr v-if="game.mentor">
          <th>Наставник</th>
          <td><UserLink :user="game.mentor" hide-badge /></td>
        </tr>
        <tr v-if="game.system">
          <th>Система</th>
          <td>{{ game.system }}</td>
        </tr>
        <tr v-if="game.setting">
          <th>Сеттинг</th>
          <td>{{ game.setting }}</td>
        </tr>
        <tr v-if="tags.length">
          <th>Теги</th>
          <td>
            <template v-for="(tag, i) in tags" :key="tag.id"
              ><router-link
                :to="{ name: 'games', query: { requiredTags: String(tag.id) } }"
                >{{ tag.title }}</router-link
              ><span v-if="i < tags.length - 1">, </span></template
            >
          </td>
        </tr>
        <tr v-if="readers.length">
          <th>Читатели</th>
          <td>
            <template v-for="(r, i) in readers" :key="r.id"
              ><UserLink :user="r" hide-badge /><span
                v-if="i < readers.length - 1"
                >,
              </span></template
            >
          </td>
        </tr>
        <tr>
          <th>Постов мастера/всего</th>
          <td>
            {{ game.masterPostsCount ?? 0 }}/{{ game.totalPostsCount ?? 0 }}
          </td>
        </tr>
        <tr>
          <th>Оцененных постов</th>
          <td>
            <router-link
              :to="{ name: 'game-post-reviews', params: { id: game.publicId } }"
              >{{ game.postReviewsCount ?? 0 }}</router-link
            >
          </td>
        </tr>
        <tr v-if="game.lastMasterPostUtc">
          <th>Последний пост</th>
          <td>{{ formatDateFull(game.lastMasterPostUtc) }}</td>
        </tr>
      </tbody>
    </table>

    <template v-for="roster in rosters" :key="roster.title">
      <section v-if="roster.characters.length" class="roster">
        <BlockTitle>{{ roster.title }}</BlockTitle>
        <DataTable
          :columns="roster.columns"
          :data="roster.characters"
          show-row-numbers
        >
          <template #cell-player="{ row }">
            <UserLink v-if="row.author" :user="row.author" hide-badge />
            <span v-else class="muted">{{ VALUE_UNAVAILABLE }}</span>
          </template>
          <template #cell-rating="{ row }">
            <UserRating
              v-if="row.author"
              :user="row.author"
              :rating="row.authorRating"
            />
            <span v-else class="muted">{{ VALUE_UNAVAILABLE }}</span>
          </template>
          <template #cell-presence="{ row }">
            <span
              v-if="row.author"
              :class="isOnline(row.author) ? 'online' : 'offline'"
              >{{ isOnline(row.author) ? "online" : "offline" }}</span
            >
          </template>
          <template #cell-character="{ row }">
            <router-link
              :to="characterLink(row)"
              :class="{ retired: isRetired(row) }"
              >{{ row.name }}</router-link
            >
          </template>
          <template #cell-descriptor="{ row }">{{
            descriptorOf(row)
          }}</template>
          <template #cell-posts="{ row }">{{ row.totalPostsCount }}</template>
          <template #cell-lastpost="{ row }">{{ lastPostOf(row) }}</template>
          <template #cell-status="{ row }"
            ><span :class="statusClass(row)">{{
              characterStatusLabel(row) ?? "В игре"
            }}</span></template
          >
        </DataTable>
      </section>
    </template>

    <section v-if="game.info" class="description">
      <DashSeparator />
      <ContentText :html="game.info" />
    </section>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Tables" as *

.game-details
  display: flex
  flex-direction: column
  gap: $big

.info-table
  +info-table

// A name longer than the column breaks inside itself rather than widen the page:
// the master, the assistants, the mentor and the readers alike. The tables of
// characters keep their names whole.
.info-table :deep(.user-link)
  overflow-wrap: anywhere

.roster
  display: flex
  flex-direction: column
  gap: $small

.offline,
.status-retired,
.retired
  color: $text-muted

.online
  color: $accent-green

// "В игре" is one state: never "В" alone at the end of a line.
.status-active
  white-space: nowrap
</style>
