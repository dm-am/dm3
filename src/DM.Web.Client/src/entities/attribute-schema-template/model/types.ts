/**
 * Starting character sheets the site offers when a game is created.
 *
 * A template is a copy source and nothing more: a game that starts from one
 * keeps its own sheet afterwards, and renaming or removing the template later
 * reaches no game.
 */
import type { AttributeSpecification } from "@/shared/api/models/game/attributeSchemas";

export interface AttributeSchemaTemplate {
  id: string;
  title: string;
  /**
   * Whether the creation form starts from this one. Read-only here: it is moved
   * by an action of its own, not by writing the template.
   */
  isDefault: boolean;
  specifications: AttributeSpecification[];
}

/**
 * The whole writable part of a template, for both creation and replacement:
 * both write every field, so one shape answers for both.
 */
export interface AttributeSchemaTemplateRequest {
  title: string;
  /** Every identifier inside is handed out by the server. */
  specifications: AttributeSpecification[];
}
