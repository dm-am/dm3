/**
 * Cross-import surface for features/create-game (FSD @x).
 *
 * Creating a game embeds the attribute-schema editor. Only the editor itself
 * goes through this door — the pure schema helpers it used to share now live in
 * entities/game, where a page can reach them by importing downwards.
 */
export { AttributeSchemaEditor } from "..";
