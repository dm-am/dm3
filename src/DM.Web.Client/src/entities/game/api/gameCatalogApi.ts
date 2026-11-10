import type { ClosedReason, ListEnvelope } from "@/shared/api/models/common";
import type { Game, GameRef } from "../model/gameTypes";
import { SIDEBAR_GAME_ROWS } from "../model/sidebarRows";
import { Api } from "@/shared/api";
import type { RequestOptions } from "@/shared/api/interceptor";

export interface GamesSearchParams {
  search?: string;
  status?: string;
  /** Narrows the Active games. */
  recruitmentFilter?: "open" | "initial" | "subsequent" | "closed";
  /** Narrows the Closed games. */
  closedReasonFilter?: ClosedReason;
  /** The game has all of them. */
  requiredTags?: number[];
  /** The game has none of them. */
  excludedTags?: number[];
  masterUsernames?: string[];
  assistantUsernames?: string[];
  /** The master or an assistant. */
  hostUsernames?: string[];
  playerUsername?: string;
  /** "Any" adds retired characters and applications under review; declined ones never match. */
  playerParticipation?: "Active" | "Any";
  /** The games the signed-in viewer takes part in, in any role. */
  participating?: boolean;
  createdFromUtc?: string;
  createdToUtc?: string;
  activatedFromUtc?: string;
  activatedToUtc?: string;
  closedFromUtc?: string;
  closedToUtc?: string;
  recruitmentStartedFromUtc?: string;
  recruitmentStartedToUtc?: string;
  sortBy?: string;
  sortOrder?: string;
  /** 1-based page number. */
  number?: number;
  size?: number;
}

const gameCatalogApi = {
  searchGames(params: GamesSearchParams = {}, options?: RequestOptions) {
    const queryParams: Record<
      string,
      string | number | boolean | string[] | number[] | undefined
    > = {};

    if (params.search) queryParams.search = params.search;
    if (params.status) queryParams.statuses = [params.status];
    if (params.recruitmentFilter)
      queryParams.recruitmentFilter = params.recruitmentFilter;
    if (params.closedReasonFilter)
      queryParams.closedReasonFilter = params.closedReasonFilter;
    if (params.requiredTags?.length)
      queryParams.requiredTags = params.requiredTags;
    if (params.excludedTags?.length)
      queryParams.excludedTags = params.excludedTags;
    if (params.masterUsernames?.length)
      queryParams.masterUsernames = params.masterUsernames;
    if (params.assistantUsernames?.length)
      queryParams.assistantUsernames = params.assistantUsernames;
    if (params.hostUsernames?.length)
      queryParams.hostUsernames = params.hostUsernames;
    if (params.playerUsername)
      queryParams.playerUsername = params.playerUsername;
    if (params.playerParticipation)
      queryParams.playerParticipation = params.playerParticipation;
    if (params.participating) queryParams.participating = true;

    if (params.createdFromUtc)
      queryParams.createdFromUtc = params.createdFromUtc;
    if (params.createdToUtc) queryParams.createdToUtc = params.createdToUtc;
    if (params.activatedFromUtc)
      queryParams.activatedFromUtc = params.activatedFromUtc;
    if (params.activatedToUtc)
      queryParams.activatedToUtc = params.activatedToUtc;
    if (params.closedFromUtc) queryParams.closedFromUtc = params.closedFromUtc;
    if (params.closedToUtc) queryParams.closedToUtc = params.closedToUtc;
    if (params.recruitmentStartedFromUtc)
      queryParams.recruitmentStartedFromUtc = params.recruitmentStartedFromUtc;
    if (params.recruitmentStartedToUtc)
      queryParams.recruitmentStartedToUtc = params.recruitmentStartedToUtc;

    // Always sent, so the order is stable.
    queryParams.sortBy = params.sortBy || "created";
    queryParams.sortOrder = params.sortOrder || "desc";

    const pageSize = params.size || 20;
    queryParams.take = pageSize;

    if (params.number && params.number > 1) {
      queryParams.skip = (params.number - 1) * pageSize;
    }

    return Api.get<ListEnvelope<Game>>(
      "games",
      queryParams,
      undefined,
      options,
    );
  },

  getParticipatingGames() {
    return Api.get<ListEnvelope<GameRef>>("games", {
      participating: true,
      projection: "ref",
    });
  },

  getModerationGames() {
    return Api.get<ListEnvelope<Game>>("games", { statuses: ["Draft"] });
  },

  getActiveGames(take: number = SIDEBAR_GAME_ROWS.active) {
    return Api.get<ListEnvelope<GameRef>>("games", {
      statuses: ["Active"],
      recruitmentFilter: "closed",
      take,
      projection: "ref",
    });
  },

  getRecruitingGames(take: number = SIDEBAR_GAME_ROWS.recruiting) {
    return Api.get<ListEnvelope<GameRef>>("games", {
      statuses: ["Active"],
      recruitmentFilter: "open",
      take,
      projection: "ref",
    });
  },

  getFinishedGames(take: number = SIDEBAR_GAME_ROWS.finished) {
    return Api.get<ListEnvelope<GameRef>>("games", {
      statuses: ["Closed"],
      closedReasonFilter: "Finished",
      take,
      projection: "ref",
    });
  },

  getPopularGames() {
    return Api.get<ListEnvelope<GameRef>>("games", {
      sortBy: "popularity",
      sortOrder: "desc",
      take: 10,
      projection: "ref",
    });
  },
};

export default gameCatalogApi;
