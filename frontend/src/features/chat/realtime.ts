import * as signalR from '@microsoft/signalr'
import { getAuthToken } from '../../shared/api/httpClient'
import type { ChatMessage } from './types'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? ''

function hubUrl(path: string) {
  return `${API_BASE_URL.replace(/\/$/, '')}${path}`
}

type ChatRealtimeHandlers = {
  onMessageReceived: (message: ChatMessage) => void
  onMessageRecalled: (message: ChatMessage) => void
}

export function createChatConnection(handlers: ChatRealtimeHandlers) {
  const connection = new signalR.HubConnectionBuilder()
    .withUrl(hubUrl('/hubs/chat'), {
      accessTokenFactory: () => getAuthToken() ?? '',
    })
    .withAutomaticReconnect()
    .build()

  connection.on('messageReceived', handlers.onMessageReceived)
  connection.on('messageRecalled', handlers.onMessageRecalled)

  return connection
}

export async function joinChatRooms(connection: signalR.HubConnection, roomIds: string[]) {
  if (connection.state !== signalR.HubConnectionState.Connected || roomIds.length === 0) {
    return
  }
  await connection.invoke('JoinRooms', roomIds)
}
