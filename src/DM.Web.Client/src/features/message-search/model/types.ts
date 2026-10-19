// The endpoint can search every source the reader may read; a field above a feed searches that feed.
export type MessageSearchScope = "global" | `dm:${string}`;

export type MessageSearchResult = {
  id: string;
  createdUtc: string;
  /** Segments, not markup: the server marks the match, and the client prints text nodes. */
  snippet: SnippetSegment[];
};

type SnippetSegment = {
  text: string;
  isMatch: boolean;
};

export type SearchMessagesParams = {
  search: string;
  in?: string[];
  cursor?: string;
  limit?: number;
};
