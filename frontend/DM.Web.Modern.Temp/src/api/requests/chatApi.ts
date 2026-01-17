import type { ListEnvelope, Envelope, PagingQuery } from "@/api/models/common";
import type { ChatMessage } from "@/api/models/chat";
import Api from "@/api";

export default new (class ChatApi {
  public getMessages(q: PagingQuery) {
    return Api.get<ListEnvelope<ChatMessage>>("globalchat/messages", q);
  }

  public getLogsByDate(date: string) {
    return Api.get<ListEnvelope<ChatMessage>>(`globalchat/logs/${date}`);
  }

  public getFirstMessageOnOrAfterDate(date: string) {
    return Api.get<Envelope<ChatMessage>>(`globalchat/logs/${date}/first`);
  }

  public getMessagesBefore(messageId: string, count: number = 50) {
    return Api.get<ListEnvelope<ChatMessage>>(`globalchat/messages/${messageId}/before`, { count });
  }

  public getMessagesAfter(messageId: string, count: number = 50) {
    return Api.get<ListEnvelope<ChatMessage>>(`globalchat/messages/${messageId}/after`, { count });
  }

  public getMessagesAround(messageId: string, count: number = 50) {
    return Api.get<ListEnvelope<ChatMessage>>(`globalchat/messages/${messageId}/around`, { count });
  }

  public sendMessage(text: string) {
    return Api.post<Envelope<ChatMessage>>("globalchat/messages", { text });
  }

  public getMessage(id: string) {
    return Api.get<Envelope<ChatMessage>>(`globalchat/messages/${id}`);
  }

  public updateMessage(id: string, text: string) {
    return Api.patch<Envelope<ChatMessage>>(`globalchat/messages/${id}`, { text });
  }

  public deleteMessage(id: string) {
    return Api.delete(`globalchat/messages/${id}`);
  }

  public likeMessage(id: string) {
    return Api.post<Envelope<ChatMessage>>(`globalchat/messages/${id}/likes`);
  }

  public unlikeMessage(id: string) {
    return Api.delete(`globalchat/messages/${id}/likes`);
  }
})();
