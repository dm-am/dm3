import type { ListEnvelope, Envelope, PagingQuery } from "@/api/models/common";
import type { Conversation, ConversationId, Message, MessageId } from "@/api/models/messaging";
import type { UserLogin } from "@/api/models/community";
import type { Patch } from "@/api/models";
import Api from "@/api";

export default new (class MessagingApi {
  // Conversations
  public getConversations(q: PagingQuery) {
    return Api.get<ListEnvelope<Conversation>>("conversations", q);
  }

  public async getDirectConversation(login: UserLogin) {
    // Backend returns 302 redirect to /conversations/{id}, axios follows it automatically
    return Api.get<Envelope<Conversation>>(`conversations/direct/${login}`);
  }

  public getConversation(id: ConversationId) {
    return Api.get<Envelope<Conversation>>(`conversations/${id}`);
  }

  public markConversationAsRead(id: ConversationId) {
    return Api.delete(`conversations/${id}/messages/unread`);
  }

  // Messages
  public getMessages(conversationId: ConversationId, q: PagingQuery) {
    return Api.get<ListEnvelope<Message>>(`conversations/${conversationId}/messages`, q);
  }

  public sendMessage(conversationId: ConversationId, text: string) {
    return Api.post<Envelope<Message>>(`conversations/${conversationId}/messages`, { text });
  }

  public getMessage(id: MessageId) {
    return Api.get<Envelope<Message>>(`messages/${id}`);
  }

  public updateMessage(id: MessageId, message: Patch<Message>) {
    return Api.patch<Envelope<Message>>(`messages/${id}`, message);
  }

  public deleteMessage(id: MessageId) {
    return Api.delete(`messages/${id}`);
  }

  // Message likes
  public likeMessage(id: MessageId) {
    return Api.post<Envelope<Message>>(`messages/${id}/likes`);
  }

  public unlikeMessage(id: MessageId) {
    return Api.delete(`messages/${id}/likes`);
  }
})();
