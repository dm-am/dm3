/**
 * Tiptap Mention Extension
 *
 * Renders [mention]login[/mention] as the link to the user's profile the
 * server draws, "@login", and keeps it one piece: without a node of its own
 * the visual mode read the anchor as text, and the next save turned the
 * mention into the plain words "@login". There is no button for it; the node
 * only carries what the author wrote in the BBCode mode through the visual one.
 */

import { Node } from "@tiptap/core";

export interface BbMentionOptions {
  HTMLAttributes: Record<string, unknown>;
}

export const BbMention = Node.create<BbMentionOptions>({
  name: "bbMention",

  addOptions() {
    return {
      HTMLAttributes: {},
    };
  },

  group: "inline",

  inline: true,

  atom: true,

  selectable: true,

  draggable: false,

  addAttributes() {
    return {
      user: {
        default: "",
        parseHTML: (element) => element.getAttribute("data-bb-user") ?? "",
        renderHTML: () => ({}),
      },
      source: {
        default: null,
        parseHTML: (element) => element.getAttribute("data-bb-source"),
        renderHTML: () => ({}),
      },
    };
  },

  parseHTML() {
    return [{ tag: 'a[data-bb-tag="mention"]' }, { tag: "a.bb-mention" }];
  },

  renderHTML({ node }) {
    const user = node.attrs.user as string;
    const source = node.attrs.source as string | null;
    return [
      "a",
      {
        class: "bb-mention",
        href: `/users/${encodeURIComponent(user)}`,
        "data-bb-tag": "mention",
        "data-bb-user": user,
        ...(source === null ? {} : { "data-bb-source": source }),
      },
      `@${user}`,
    ];
  },
});

export default BbMention;
