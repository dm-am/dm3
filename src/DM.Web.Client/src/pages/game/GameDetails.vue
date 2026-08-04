<script setup lang="ts">
// Game info block ("Информация игры", dev doc 4.2.2.9): a key-value fact
// table, the player + master character rosters, and the description — the old
// site's module-info layout, but the fields/columns follow the doc, not the
// legacy page (e.g. "Посты"/"Последний пост", not "Ходов"/"Последний ход"; the
// descriptor column is titled by the schema's descriptor attribute).
import { computed } from "vue";
import { storeToRefs } from "pinia";
import {
  useGameDetailsStore,
  GameStatusBadge,
  type Character,
} from "@/entities/game";
import { UserLink, UserRating, useUserDisplay } from "@/entities/user";
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
const recruitment = computed(() => game.value?.recruitment);

// "Количество игроков: X/Y" — accepted players / limit (doc). Without a limit
// only the accepted count is shown.
const playerCount = computed(() => {
  const r = recruitment.value;
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

// The descriptor column is titled by the schema's descriptor attribute
// ("[Дескриптор]. По умолчанию 'класс'").
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

// Character status, refined from the Retired flags.
function statusLabel(c: Character): string {
  if (c.status === "Active") return "В игре";
  if (c.status === "Retired") {
    if (c.isDead) return "Персонаж мертв";
    if (c.isPlayerExiled) return "Выведен из игры";
    if (c.isPlayerLeft) return "Покинул игру";
    return "Вне игры";
  }
  return c.status;
}
function isRetired(c: Character): boolean {
  return c.status === "Retired";
}

// The name of a character leads to that character's own page. It used to lead
// to the roster of the whole game with a scrollTo query, which pointed at the
// right card only in intent: no roster reads that query. The name inside a post
// leads to the same page, so one name never means two places.
function characterLink(c: Character) {
  return {
    name: "game-character",
    params: {
      id: game.value?.publicId || game.value?.id,
      characterId: c.id,
    },
  };
}

// Character status colour — "В игре" green, everything else muted (old site).
function statusClass(c: Character): string {
  return c.status === "Active" ? "status-active" : "status-retired";
}

// Roster columns, every one centred as on the old site. Both tables use layout
// "auto" instead of a percentage width on the content-sized columns: a date
// ("DD.MM.YYYY в HH:mm") and the "Присутствие" header each need a constant
// number of pixels, and no single percentage covers both a 1600 and a 1920
// viewport. At 12% the date wrapped onto a second line at either width, and
// the wrap grew every roster row. So "Последний пост" and "Присутствие" are
// nowrap and width-less (they take exactly their content), "Игрок" and "Имя
// персонажа" are width-less too and absorb the rest, and the remaining columns
// keep their proportions as hints.
const playerColumns = computed<Column[]>(() => [
  { key: "player", label: "Игрок", align: "center" },
  {
    key: "rating",
    label: "Рейтинг",
    align: "center",
    width: "11%",
    hideOnMobile: true,
  },
  {
    key: "presence",
    label: "Присутствие",
    align: "center",
    hideOnMobile: true,
  },
  { key: "character", label: "Имя персонажа", align: "center" },
  {
    key: "descriptor",
    label: descriptorTitle.value,
    align: "center",
    width: "11%",
  },
  { key: "posts", label: "Посты", align: "center", width: "7%" },
  {
    key: "lastpost",
    label: "Последний пост",
    align: "center",
    hideOnMobile: true,
  },
  { key: "status", label: "Статус", align: "center", width: "11%" },
]);
const npcColumns = computed<Column[]>(() => [
  { key: "character", label: "Имя персонажа", align: "center" },
  {
    key: "descriptor",
    label: descriptorTitle.value,
    align: "center",
    width: "18%",
  },
  { key: "posts", label: "Посты", align: "center", width: "12%" },
  {
    key: "lastpost",
    label: "Последний пост",
    align: "center",
    hideOnMobile: true,
  },
  { key: "status", label: "Статус", align: "center", width: "18%" },
]);
</script>

<template>
  <div v-if="game" class="game-details">
    <!-- Key-value info table (dev doc 4.2.2.9), a borderless information block. -->
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
          <td><UserLink :user="game.master" hide-badge /></td>
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
                class="tag-link"
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
        <!-- The home of the post ratings, which the games listing no longer
             shows: they address posts of this game, so they belong to the
             game's facts and not to a column of a list of games. -->
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

    <!-- Player roster -->
    <section v-if="playerCharacters.length" class="roster">
      <BlockTitle>Персонажи игроков</BlockTitle>
      <DataTable
        :columns="playerColumns"
        :data="playerCharacters"
        :show-row-numbers="true"
        empty-text="Персонажей пока нет"
        table-layout="auto"
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
          <!-- No author, no page for the link to lead to. -->
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
        <template #cell-descriptor="{ row }">{{ descriptorOf(row) }}</template>
        <template #cell-posts="{ row }">{{ row.totalPostsCount }}</template>
        <template #cell-lastpost="{ row }">{{ lastPostOf(row) }}</template>
        <template #cell-status="{ row }"
          ><span :class="statusClass(row)">{{
            statusLabel(row)
          }}</span></template
        >
      </DataTable>
    </section>

    <!-- Master's characters (NPC) -->
    <section v-if="npcCharacters.length" class="roster">
      <BlockTitle>Персонажи мастера</BlockTitle>
      <DataTable
        :columns="npcColumns"
        :data="npcCharacters"
        :show-row-numbers="true"
        empty-text="Персонажей мастера пока нет"
        table-layout="auto"
      >
        <template #cell-character="{ row }">
          <router-link
            :to="characterLink(row)"
            :class="{ retired: isRetired(row) }"
            >{{ row.name }}</router-link
          >
        </template>
        <template #cell-descriptor="{ row }">{{ descriptorOf(row) }}</template>
        <template #cell-posts="{ row }">{{ row.totalPostsCount }}</template>
        <template #cell-lastpost="{ row }">{{ lastPostOf(row) }}</template>
        <template #cell-status="{ row }"
          ><span :class="statusClass(row)">{{
            statusLabel(row)
          }}</span></template
        >
      </DataTable>
    </section>

    <!-- Description (BBCode, server-rendered) -->
    <section v-if="game.info" class="description">
      <DashSeparator />
      <ContentText :html="game.info" />
    </section>
  </div>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Tables"

.game-details
  display: flex
  flex-direction: column
  gap: $big

.info-table
  +info-table

.tag-link
  color: $link
  &:hover
    color: $link-hover

.roster
  display: flex
  flex-direction: column
  gap: $small

.muted
  color: $text-muted

.online
  color: $accent-green

.offline
  color: $text-muted

.status-active
  color: $accent-green

.status-retired
  color: $text-muted

// Retired characters read as muted in the roster (dead / left / exiled).
.retired
  color: $text-muted

// The two content-sized roster columns. With the auto table layout each takes
// exactly the width its content needs on one line: the date of the last post,
// and "Присутствие", where the widest thing in the column is the header itself
// and not the "online"/"offline" value under it.
:deep(.col-lastpost),
:deep(.col-presence)
  white-space: nowrap
</style>
