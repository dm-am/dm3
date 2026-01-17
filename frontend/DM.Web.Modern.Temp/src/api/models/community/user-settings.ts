export enum ColorSchema {
  Light = "Light",
  Dark = "Dark",
}

export type PagingLimits = {
  postsPerPage: number;
  commentsPerPage: number;
  topicsPerPage: number;
  messagesPerPage: number;
  entitiesPerPage: number;
};

export type UserSettings = {
  mentorGreetingsMessage: string;
  colorSchema: ColorSchema;
  pagingLimits: PagingLimits | null;
};
