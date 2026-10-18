import type { Envelope, ListEnvelope } from "@/shared/api/models/common";
import type {
  AttributeSchemaTemplate,
  AttributeSchemaTemplateRequest,
} from "../model/types";
import { Api } from "@/shared/api";

/**
 * The refusal a caller takes over because its own screen carries it.
 *
 * A screen that answers a failure where the reader is looking — the strip in
 * place of a list, the card in place of a sheet — is the one carrier of that
 * failure, and the response interceptor has to stay quiet or the same sentence
 * stands in two places at once. The methods below pass this through untouched;
 * every call that leaves it out is unchanged, because the client then sees
 * nothing and the interceptor speaks as it always did.
 *
 * Declared here rather than imported: `shared/api` keeps its own copy of the
 * type to itself, and the client reads the two fields off the request by name,
 * so a structural twin carries them just as well.
 */
type RefusalFlags = {
  /** The screen shows a 403 and a 5xx itself. */
  ownsRefusal?: boolean;
  /** The screen shows a 429 itself, the wait included. */
  ownsRateLimit?: boolean;
};

/**
 * The catalogue of starting character sheets.
 *
 * Reading is open to everyone, because the game creation form fills itself from
 * it; writing answers to senior moderation, and the server says so rather than
 * this module.
 */
export default new (class AttributeSchemaTemplateApi {
  /**
   * The whole catalogue, every entry with its specifications, so the creation
   * form opens on one request. The default template comes first.
   *
   * @param options Set by a caller whose screen shows the failure itself: the
   * moderation catalogue draws a failed load where the list would be. The game
   * creation form reads the same catalogue and sets nothing, so its refusal
   * stays with the interceptor.
   */
  public getTemplates(options?: RefusalFlags) {
    return Api.get<ListEnvelope<AttributeSchemaTemplate>>(
      "attribute-schema-templates",
      undefined,
      undefined,
      options,
    );
  }

  /**
   * One template, for the page that is opened by its address.
   *
   * @param options As in getTemplates — the page that edits a template draws a
   * failed read in place of the editor.
   */
  public getTemplate(id: string, options?: RefusalFlags) {
    return Api.get<Envelope<AttributeSchemaTemplate>>(
      `attribute-schema-templates/${id}`,
      undefined,
      undefined,
      options,
    );
  }

  public createTemplate(template: AttributeSchemaTemplateRequest) {
    return Api.post<Envelope<AttributeSchemaTemplate>>(
      "attribute-schema-templates",
      template,
    );
  }

  /** Replaces every writable field; the default flag is not one of them. */
  public updateTemplate(id: string, template: AttributeSchemaTemplateRequest) {
    return Api.put<Envelope<AttributeSchemaTemplate>>(
      `attribute-schema-templates/${id}`,
      template,
    );
  }

  /** @param options Set by the window that asks before the removal. */
  public deleteTemplate(id: string, options?: RefusalFlags) {
    return Api.delete(`attribute-schema-templates/${id}`, undefined, options);
  }

  /** Moves the default flag onto this template. */
  public makeDefault(id: string) {
    return Api.post(`attribute-schema-templates/${id}/default`);
  }
})();
